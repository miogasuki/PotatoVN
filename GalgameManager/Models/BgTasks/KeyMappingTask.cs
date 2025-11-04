using System.Diagnostics;
using System.Runtime.InteropServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Windows.System;
using WindowsInput;
using WindowsInput.Native;

namespace GalgameManager.Models.BgTasks;

public class KeyMappingTask : BgTaskBase
{
    public string ProcessName { get; set; } = string.Empty;
    public Galgame? Galgame;
    public override bool ProgressOnTrayIcon => false;
    public List<KeyMapping> KeyMappings { get; set; } = new();

    private Process? _process;
    private nint _hookId = nint.Zero;
    private readonly IInputSimulator _inputSimulator = new InputSimulator();
    private readonly Dictionary<string, (List<VirtualKeyCode> modifiers, VirtualKeyCode key)> _lookupMap = new();

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);
    private LowLevelKeyboardProc? _hookProc;

    #region P_INVOKE
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
    #endregion
    
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    public KeyMappingTask() { }

    public KeyMappingTask(Galgame game, Process process)
    {
        ProcessName = process.ProcessName;
        Galgame = game;
        _process = process;
        KeyMappings = game.KeyMappings.Select(m => new KeyMapping
        {
            From = new List<int>(m.From),
            To = new List<int>(m.To),
            IsEnabled = m.IsEnabled,
            Remark = m.Remark
        }).ToList();
    }

    protected override Task RecoverFromJsonInternal()
    {
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    { 
        if (_process is null || Galgame is null) return;
        
        BuildLookupMap();
        if (_lookupMap.Count == 0) return;

        ChangeProgress(0, 1, "KeyMappingTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
        
        _hookProc = HookCallback;
        _hookId = SetHook(_hookProc);
        
        try
        {
            await _process.WaitForExitAsync();
        }
        finally
        {
            if (_hookId != nint.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = nint.Zero;
            }
            _hookProc = null;
            ChangeProgress(1, 1, "KeyMappingTask_Done".GetLocalized());
        }
    }

    private void BuildLookupMap()
    {
        foreach (var mapping in KeyMappings)
        {
            if (!mapping.IsEnabled || mapping.From.Count == 0 || mapping.To.Count == 0) continue;

            var fromKeys = mapping.From.Select(k => (VirtualKey)k).ToList();
            fromKeys.Sort((a, b) => GetKeyOrder(a) - GetKeyOrder(b));
            var fromKeyString = string.Join("+", fromKeys.Select(GetKeyDisplayName));

            var toKeys = mapping.To.Select(k => (VirtualKey)k).ToList();
            var toModifiers = toKeys.Where(IsModifierKey).Select(k => (VirtualKeyCode)k).ToList();
            var toKey = toKeys.FirstOrDefault(k => !IsModifierKey(k));
            if (toKey == default)
            {
                toKey = toKeys.FirstOrDefault();
            }

            _lookupMap[fromKeyString] = (toModifiers, (VirtualKeyCode)toKey);
        }
    }

    private nint SetHook(LowLevelKeyboardProc proc)
    {
        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule curModule = curProcess.MainModule!;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName!), 0);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hookId, nCode, wParam, lParam);
        
        try
        {
            if (_process is null || _process.HasExited) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            var foregroundWindowHandle = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundWindowHandle, out var foregroundProcessId);
            if (_process.Id != foregroundProcessId) return CallNextHookEx(_hookId, nCode, wParam, lParam);
            
            if (wParam != WM_KEYDOWN && wParam != WM_SYSKEYDOWN) return CallNextHookEx(_hookId, nCode, wParam, lParam);
            
            var vkCode = (VirtualKey)Marshal.ReadInt32(lParam);
            if (IsModifierKey(vkCode)) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            List<VirtualKey> pressedKeys = new() { vkCode };
            if (IsKeyDown(VirtualKey.Control)) pressedKeys.Add(VirtualKey.Control);
            if (IsKeyDown(VirtualKey.Shift)) pressedKeys.Add(VirtualKey.Shift);
            if (IsKeyDown(VirtualKey.Menu)) pressedKeys.Add(VirtualKey.Menu); // Alt
            if (IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows)) pressedKeys.Add(VirtualKey.LeftWindows);

            pressedKeys.Sort((a, b) => GetKeyOrder(a) - GetKeyOrder(b));
            var keyString = string.Join("+", pressedKeys.Select(GetKeyDisplayName));

            if (_lookupMap.TryGetValue(keyString, out var toHotkey))
            {
                _inputSimulator.Keyboard.ModifiedKeyStroke(toHotkey.modifiers, toHotkey.key);
                return 1; 
            }
        }
        catch(Exception)
        {
            // Ignored
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
    
    private bool IsKeyDown(VirtualKey key) => (GetKeyState((int)key) & 0x8000) != 0;

    private static bool IsModifierKey(VirtualKey key) => key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or VirtualKey.Menu or VirtualKey.LeftMenu 
        or VirtualKey.RightMenu or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static int GetKeyOrder(VirtualKey key)
    {
        if (key is VirtualKey.LeftWindows or VirtualKey.RightWindows) return 1;
        if (key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl) return 2;
        if (key is VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu) return 3;
        if (key is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift) return 4;
        return 5;
    }
    
    private static string GetKeyDisplayName(VirtualKey key) => key switch
    {
        VirtualKey.LeftWindows or VirtualKey.RightWindows => "Win",
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => "Ctrl",
        VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => "Alt",
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => "Shift",
        _ => key.ToString()
    };

    public override string Title { get; } = "KeyMappingTask_Title".GetLocalized();
}