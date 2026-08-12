using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Windows.System;

namespace GalgameManager.Models.BgTasks;

public class KeyMappingTask : BgTaskBase
{
    public string ProcessName { get; set; } = string.Empty;
    public Galgame? Galgame;
    public override bool ProgressOnTrayIcon => false;
    public List<KeyMapping> KeyMappings { get; set; } = new();

    private Process? _process;
    private string[] _directoryPrefixes = [];
    private readonly HashSet<int> _trackedProcessIds = [];
    private readonly Dictionary<string, OutputAction> _lookupMap = new();
    private readonly Dictionary<int, OutputAction> _activeKeyboardMappings = new();
    private readonly Dictionary<int, OutputAction> _activeMouseMappings = new();
    private nint _keyboardHookId;
    private nint _mouseHookId;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private readonly ManualResetEventSlim _hookStarted = new(false);
    private Exception? _hookStartException;
    private int _callbackErrorLogged;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);
    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);
    private LowLevelKeyboardProc? _keyboardHookProc;
    private LowLevelMouseProc? _mouseHookProc;

    #region P_INVOKE

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int nVirtKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, NativeInput[] inputs, int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out NativeMessage lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMessage lpMsg, nint hWnd, uint wMsgFilterMin,
        uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref NativeMessage lpMsg);

    #endregion

    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const uint WmQuit = 0x0012;
    private const uint LlkhfExtended = 0x01;
    private const uint LlkhfInjected = 0x10;
    private const uint LlmhfInjected = 0x01;

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;
    private const uint MouseEventWheel = 0x0800;

    public KeyMappingTask()
    {
    }

    public KeyMappingTask(Galgame game, Process process)
        : this(game, process, game.KeyMappings)
    {
    }

    public KeyMappingTask(Galgame game, Process process, IEnumerable<KeyMapping> keyMappings)
    {
        ProcessName = process.ProcessName;
        Galgame = game;
        _process = process;
        _trackedProcessIds.Add(GameProcessDetector.SafeGetId(process));
        KeyMappings = keyMappings.Select(m => new KeyMapping
        {
            From = new List<int>(m.From),
            To = new List<int>(m.To),
            IsEnabled = m.IsEnabled,
            Remark = m.Remark,
            IsGlobal = m.IsGlobal,
        }).ToList();
        InitDirectoryPrefixes();
    }

    protected override Task RecoverFromJsonInternal()
    {
        _process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        if (_process is not null) _trackedProcessIds.Add(GameProcessDetector.SafeGetId(_process));
        InitDirectoryPrefixes();
        return Task.CompletedTask;
    }

    protected override async Task RunInternal()
    {
        if (_process is null || Galgame is null) return;

        BuildLookupMap();
        if (_lookupMap.Count == 0) return;

        ChangeProgress(0, 1, "KeyMappingTask_ProgressMsg".GetLocalized(Galgame.Name.Value!));
        try
        {
            await Task.Run(StartHookThread);
            await FollowGameProcessAsync();
        }
        finally
        {
            await Task.Run(StopHookThread);
            ChangeProgress(1, 1, "KeyMappingTask_Done".GetLocalized());
        }
    }

    private void InitDirectoryPrefixes()
    {
        _directoryPrefixes = Galgame?.SourceEntries
            .Select(entry => entry.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => GameProcessDetector.GetDirectoryPrefix(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private async Task FollowGameProcessAsync()
    {
        while (_process is not null)
        {
            try
            {
                await _process.WaitForExitAsync();
            }
            catch
            {
                // ShellExecute 和快捷方式返回的 Process 可能无法等待，交给目录探测兜底。
            }

            int exitedProcessId = GameProcessDetector.SafeGetId(_process);
            if (exitedProcessId > 0) _trackedProcessIds.Add(exitedProcessId);
            Process? replacement = await WaitForReplacementProcessAsync();
            if (replacement is null) break;

            _process = replacement;
            ProcessName = replacement.ProcessName;
            _trackedProcessIds.Add(replacement.Id);
        }
    }

    private async Task<Process?> WaitForReplacementProcessAsync()
    {
        if (_directoryPrefixes.Length == 0) return null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            foreach (string prefix in _directoryPrefixes)
            {
                Process? candidate = GameProcessDetector.FindBestProcessInDirectory(prefix, _trackedProcessIds);
                if (candidate is not null) return candidate;
            }
            await Task.Delay(1000);
        }
        return null;
    }

    private void BuildLookupMap()
    {
        _lookupMap.Clear();
        // Rules are already ordered by priority: global rules first, then per-game rules.
        // TryAdd preserves the first rule when malformed or legacy data still overlaps.
        foreach (KeyMapping mapping in KeyMappings)
        {
            if (!mapping.IsEnabled || mapping.From.Count == 0 || mapping.To.Count == 0) continue;

            OutputAction? output = CreateOutputAction(mapping.To);
            if (output is null) continue;

            int fromMouseButton = mapping.From.FirstOrDefault(IsMouseButtonCode);
            if (fromMouseButton != 0)
            {
                _lookupMap.TryAdd($"Mouse{fromMouseButton}", output);
                continue;
            }

            foreach (string sourceSignature in ExpandSourceSignatures(mapping.From))
                _lookupMap.TryAdd(sourceSignature, output);
        }
    }

    private static IEnumerable<string> ExpandSourceSignatures(IReadOnlyList<int> sourceKeys)
    {
        List<List<VirtualKey>> combinations = [[]];
        foreach (int sourceKey in sourceKeys)
        {
            VirtualKey key = NormalizeExactModifier((VirtualKey)sourceKey);
            VirtualKey[] variants = key switch
            {
                VirtualKey.Control => [VirtualKey.LeftControl, VirtualKey.RightControl],
                VirtualKey.Menu => [VirtualKey.LeftMenu, VirtualKey.RightMenu],
                VirtualKey.Shift => [VirtualKey.LeftShift, VirtualKey.RightShift],
                _ => [key],
            };

            List<List<VirtualKey>> expanded = [];
            foreach (List<VirtualKey> combination in combinations)
            foreach (VirtualKey variant in variants)
                expanded.Add([.. combination, variant]);
            combinations = expanded;
        }

        return combinations
            .Select(keys => string.Join("+", keys
                .Distinct()
                .OrderBy(GetKeyOrder)
                .Select(GetKeyDisplayName)))
            .Where(signature => signature.Length > 0)
            .Distinct(StringComparer.Ordinal);
    }

    private static OutputAction? CreateOutputAction(IReadOnlyList<int> keys)
    {
        int mouseButton = keys.FirstOrDefault(IsMouseButtonCode);
        if (mouseButton != 0) return new OutputAction([], null, mouseButton);

        List<VirtualKey> targetKeys = keys
            .Select(k => (VirtualKey)k)
            .Distinct()
            .OrderBy(GetKeyOrder)
            .ToList();
        if (targetKeys.Count == 0) return null;

        List<int> modifiers = targetKeys
            .Where(IsModifierKey)
            .Select(k => (int)k)
            .ToList();
        VirtualKey? mainKey = targetKeys.FirstOrDefault(k => !IsModifierKey(k));
        if (mainKey is null || mainKey == default)
        {
            mainKey = targetKeys[0];
            modifiers.Remove((int)mainKey.Value);
        }

        return new OutputAction(modifiers, (int)mainKey.Value, null);
    }

    private void StartHookThread()
    {
        _hookStartException = null;
        _hookStarted.Reset();
        _hookThread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "PotatoVN native key mapping hook",
        };
        _hookThread.Start();

        if (!_hookStarted.Wait(TimeSpan.FromSeconds(5)))
        {
            StopHookThread();
            throw new TimeoutException("启动键位映射钩子超时。");
        }
        if (_hookStartException is not null)
        {
            Exception inner = _hookStartException;
            StopHookThread();
            throw new InvalidOperationException("无法启动键位映射钩子。", inner);
        }
    }

    private void HookThreadMain()
    {
        try
        {
            _hookThreadId = GetCurrentThreadId();
            _ = PeekMessage(out _, nint.Zero, 0, 0, 0);

            _keyboardHookProc = KeyboardHookCallback;
            _mouseHookProc = MouseHookCallback;
            nint moduleHandle = GetModuleHandle(null);

            _keyboardHookId = SetWindowsHookEx(WhKeyboardLl, _keyboardHookProc, moduleHandle, 0);
            if (_keyboardHookId == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            _mouseHookId = SetWindowsHookEx(WhMouseLl, _mouseHookProc, moduleHandle, 0);
            if (_mouseHookId == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            _hookStarted.Set();
            while (true)
            {
                int result = GetMessage(out NativeMessage message, nint.Zero, 0, 0);
                if (result == 0) break;
                if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                _ = TranslateMessage(ref message);
                _ = DispatchMessage(ref message);
            }
        }
        catch (Exception ex)
        {
            _hookStartException ??= ex;
            _hookStarted.Set();
        }
        finally
        {
            if (_keyboardHookId != nint.Zero)
            {
                _ = UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = nint.Zero;
            }
            if (_mouseHookId != nint.Zero)
            {
                _ = UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = nint.Zero;
            }

            ReleaseAllOutputs();
            _keyboardHookProc = null;
            _mouseHookProc = null;
            _hookThreadId = 0;
        }
    }

    private void StopHookThread()
    {
        Thread? thread = _hookThread;
        if (thread is null) return;

        uint threadId = _hookThreadId;
        if (threadId != 0) _ = PostThreadMessage(threadId, WmQuit, 0, nint.Zero);
        if (Thread.CurrentThread != thread) _ = thread.Join(TimeSpan.FromSeconds(3));
        if (!thread.IsAlive) _hookThread = null;
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0) return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

        try
        {
            KbdLlHookStruct data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if ((data.Flags & LlkhfInjected) != 0)
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

            int message = unchecked((int)wParam.ToInt64());
            int sourceKey = (int)ResolvePhysicalModifier(data);
            if (message is WmKeyUp or WmSysKeyUp)
            {
                if (_activeKeyboardMappings.Remove(sourceKey, out OutputAction? active))
                {
                    ReleaseOutput(active);
                    return 1;
                }
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
            }
            if (message is not (WmKeyDown or WmSysKeyDown))
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

            if (_activeKeyboardMappings.ContainsKey(sourceKey)) return 1;
            string keyString = BuildPressedKeyString((VirtualKey)sourceKey);
            _lookupMap.TryGetValue(keyString, out OutputAction? output);

            bool targetForeground = IsTargetGameForeground(out _, out _);
            if (!targetForeground || output is null)
                return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);

            PressOutput(output);
            _activeKeyboardMappings[sourceKey] = output;
            return 1;
        }
        catch (Exception ex)
        {
            LogCallbackError(ex);
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0) return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

        try
        {
            MsllHookStruct data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            if ((data.Flags & LlmhfInjected) != 0)
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

            int message = unchecked((int)wParam.ToInt64());
            int mouseButton = GetMouseButton(message, data.MouseData);
            if (mouseButton == 0) return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

            if (IsMouseButtonUp(message))
            {
                if (_activeMouseMappings.Remove(mouseButton, out OutputAction? active))
                {
                    ReleaseOutput(active);
                    return 1;
                }
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
            }

            bool targetForeground = IsTargetGameForeground(out _, out _);
            _lookupMap.TryGetValue($"Mouse{mouseButton}", out OutputAction? output);
            if (!targetForeground || output is null)
                return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);

            if (message == WmMouseWheel)
            {
                PulseOutput(output);
                return 1;
            }
            if (_activeMouseMappings.ContainsKey(mouseButton)) return 1;

            PressOutput(output);
            _activeMouseMappings[mouseButton] = output;
            return 1;
        }
        catch (Exception ex)
        {
            LogCallbackError(ex);
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }
    }

    private bool IsTargetGameForeground(out uint foregroundProcessId, out string matchReason)
    {
        nint window = GetForegroundWindow();
        foregroundProcessId = 0;
        if (window == nint.Zero)
        {
            matchReason = "无前台窗口";
            return false;
        }
        _ = GetWindowThreadProcessId(window, out foregroundProcessId);
        if (foregroundProcessId == 0)
        {
            matchReason = "前台进程未知";
            return false;
        }

        try
        {
            if (_process is { HasExited: false } && _process.Id == foregroundProcessId)
            {
                matchReason = "跟踪进程一致";
                return true;
            }
        }
        catch
        {
            // 继续通过安装目录判断。
        }

        try
        {
            using Process foreground = Process.GetProcessById(checked((int)foregroundProcessId));
            string? executablePath = foreground.TryGetExecutablePath();
            if (executablePath is not null && _directoryPrefixes.Any(prefix =>
                    executablePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                matchReason = "位于游戏安装目录";
                return true;
            }

            if (_directoryPrefixes.Length == 0 &&
                foreground.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                matchReason = "进程名一致";
                return true;
            }

            matchReason = executablePath is null
                ? $"前台进程={foreground.ProcessName}，路径不可读"
                : $"前台进程={foreground.ProcessName}";
            return false;
        }
        catch
        {
            matchReason = "读取前台进程失败";
            return false;
        }
    }

    private string BuildPressedKeyString(VirtualKey currentKey)
    {
        List<VirtualKey> pressed = [NormalizeExactModifier(currentKey)];
        AddIfPressed(VirtualKey.LeftControl);
        AddIfPressed(VirtualKey.RightControl);
        AddIfPressed(VirtualKey.LeftShift);
        AddIfPressed(VirtualKey.RightShift);
        AddIfPressed(VirtualKey.LeftMenu);
        AddIfPressed(VirtualKey.RightMenu);
        if (IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows))
            pressed.Add(VirtualKey.LeftWindows);

        return string.Join("+", pressed
            .Select(NormalizeExactModifier)
            .Distinct()
            .OrderBy(GetKeyOrder)
            .Select(GetKeyDisplayName));

        void AddIfPressed(VirtualKey key)
        {
            if (IsKeyDown(key)) pressed.Add(key);
        }
    }

    private void PressOutput(OutputAction output)
    {
        if (output.MouseButton is { } mouseButton)
        {
            SendMouseState(mouseButton, true);
            return;
        }

        foreach (int modifier in output.Modifiers) SendKeyboardState(modifier, true);
        if (output.Key is { } key) SendKeyboardState(key, true);
    }

    private void ReleaseOutput(OutputAction output)
    {
        if (output.MouseButton is { } mouseButton)
        {
            SendMouseState(mouseButton, false);
            return;
        }

        if (output.Key is { } key) SendKeyboardState(key, false);
        for (int i = output.Modifiers.Count - 1; i >= 0; i--)
            SendKeyboardState(output.Modifiers[i], false);
    }

    private void PulseOutput(OutputAction output)
    {
        PressOutput(output);
        ReleaseOutput(output);
    }

    private void ReleaseAllOutputs()
    {
        foreach (OutputAction output in _activeKeyboardMappings.Values.Concat(_activeMouseMappings.Values))
        {
            try
            {
                ReleaseOutput(output);
            }
            catch
            {
                // 退出时尽力释放，不能阻止钩子线程结束。
            }
        }
        _activeKeyboardMappings.Clear();
        _activeMouseMappings.Clear();
    }

    private static void SendMouseState(int mouseButton, bool down)
    {
        (uint flags, uint mouseData) = mouseButton switch
        {
            1 => (down ? MouseEventLeftDown : MouseEventLeftUp, 0u),
            2 => (down ? MouseEventRightDown : MouseEventRightUp, 0u),
            3 => (down ? MouseEventMiddleDown : MouseEventMiddleUp, 0u),
            4 => (down ? MouseEventXDown : MouseEventXUp, 1u),
            5 => (down ? MouseEventXDown : MouseEventXUp, 2u),
            6 when down => (MouseEventWheel, 120u),
            7 when down => (MouseEventWheel, unchecked((uint)-120)),
            _ => (0u, 0u),
        };
        if (flags == 0) return;

        SendNativeInput(new NativeInput
        {
            Type = InputMouse,
            Data = new NativeInputUnion
            {
                Mouse = new NativeMouseInput
                {
                    MouseData = mouseData,
                    Flags = flags,
                },
            },
        });
    }

    private static void SendKeyboardState(int virtualKey, bool down)
    {
        uint flags = down ? 0 : KeyEventKeyUp;
        if (IsExtendedKey(virtualKey)) flags |= KeyEventExtendedKey;

        SendNativeInput(new NativeInput
        {
            Type = InputKeyboard,
            Data = new NativeInputUnion
            {
                Keyboard = new NativeKeyboardInput
                {
                    VirtualKey = checked((ushort)virtualKey),
                    Flags = flags,
                },
            },
        });
    }

    private static void SendNativeInput(NativeInput input)
    {
        NativeInput[] inputs = [input];
        uint sent = SendInput(1, inputs, Marshal.SizeOf<NativeInput>());
        if (sent != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput 未能发送按键映射输入。");
    }

    private static bool IsExtendedKey(int virtualKey) => virtualKey is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or
        0x2D or 0x2E or 0x5B or 0x5C or 0x6F or 0x90 or 0xA3 or 0xA5;

    private void LogCallbackError(Exception exception)
    {
        if (Interlocked.Exchange(ref _callbackErrorLogged, 1) != 0) return;
        App.GetService<IInfoService>().DeveloperEvent(
            msg: "键位映射处理输入事件时发生错误。",
            e: exception);
    }

    private static bool IsKeyDown(VirtualKey key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    private static bool IsMouseButtonCode(int code) => code is >= 1 and <= 7;

    private static bool IsMouseButtonUp(int message) =>
        message is WmLButtonUp or WmRButtonUp or WmMButtonUp or WmXButtonUp;

    private static int GetMouseButton(int message, uint mouseData) => message switch
    {
        WmLButtonDown or WmLButtonUp => 1,
        WmRButtonDown or WmRButtonUp => 2,
        WmMButtonDown or WmMButtonUp => 3,
        WmXButtonDown or WmXButtonUp => (ushort)(mouseData >> 16) == 1 ? 4 : 5,
        WmMouseWheel => unchecked((short)(mouseData >> 16)) > 0 ? 6 : 7,
        _ => 0,
    };

    private static bool IsModifierKey(VirtualKey key) => key is
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
        VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
        VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static VirtualKey NormalizeExactModifier(VirtualKey key) => key switch
    {
        VirtualKey.RightWindows => VirtualKey.LeftWindows,
        _ => key,
    };

    private static VirtualKey ResolvePhysicalModifier(KbdLlHookStruct data)
    {
        VirtualKey key = (VirtualKey)data.VkCode;
        return key switch
        {
            VirtualKey.Shift => data.ScanCode == 0x36
                ? VirtualKey.RightShift
                : VirtualKey.LeftShift,
            VirtualKey.Control => (data.Flags & LlkhfExtended) != 0
                ? VirtualKey.RightControl
                : VirtualKey.LeftControl,
            VirtualKey.Menu => (data.Flags & LlkhfExtended) != 0
                ? VirtualKey.RightMenu
                : VirtualKey.LeftMenu,
            _ => key,
        };
    }

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
        VirtualKey.Control => "Ctrl",
        VirtualKey.LeftControl => "左 Ctrl",
        VirtualKey.RightControl => "右 Ctrl",
        VirtualKey.Menu => "Alt",
        VirtualKey.LeftMenu => "左 Alt",
        VirtualKey.RightMenu => "右 Alt",
        VirtualKey.Shift => "Shift",
        VirtualKey.LeftShift => "左 Shift",
        VirtualKey.RightShift => "右 Shift",
        _ => key.ToString(),
    };

    public override string Title { get; } = "KeyMappingTask_Title".GetLocalized();

    private sealed record OutputAction(
        List<int> Modifiers,
        int? Key,
        int? MouseButton);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public NativeInputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)] public NativeMouseInput Mouse;
        [FieldOffset(0)] public NativeKeyboardInput Keyboard;
        [FieldOffset(0)] public NativeHardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}
