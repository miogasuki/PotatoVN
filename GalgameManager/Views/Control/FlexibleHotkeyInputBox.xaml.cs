using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.WinUI;

namespace GalgameManager.Views.Control;

public sealed partial class FlexibleHotkeyInputBox : INotifyPropertyChanged
{
    private readonly HashSet<VirtualKey> _pressedKeys = new();
    private readonly HashSet<int> _mouseButtons = new();
    private bool _isCapturing;
    private int _clickCount = 0;
    private string _buttonText = "";
    private string _hotkeyDisplayText = "";
    private Visibility _showPlaceholder = Visibility.Visible;
    private Visibility _hideText = Visibility.Collapsed;

    public FlexibleHotkeyInputBox()
    {
        InitializeComponent();
        UpdateButtonText();
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(FlexibleHotkeyInputBox),
            new PropertyMetadata(""));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty HotkeyKeysProperty =
        DependencyProperty.Register(nameof(HotkeyKeys), typeof(List<int>), typeof(FlexibleHotkeyInputBox),
            new PropertyMetadata(new List<int>(), OnHotkeyKeysChanged));

    public List<int> HotkeyKeys
    {
        get => (List<int>)GetValue(HotkeyKeysProperty);
        set => SetValue(HotkeyKeysProperty, value);
    }

    private static void OnHotkeyKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlexibleHotkeyInputBox control)
        {
            control.UpdateDisplay();
        }
    }

    public string ButtonText
    {
        get => _buttonText;
        private set
        {
            _buttonText = value;
            OnPropertyChanged();
        }
    }

    public string HotkeyDisplayText
    {
        get => _hotkeyDisplayText;
        private set
        {
            _hotkeyDisplayText = value;
            OnPropertyChanged();
        }
    }

    public Visibility ShowPlaceholder
    {
        get => _showPlaceholder;
        private set
        {
            _showPlaceholder = value;
            OnPropertyChanged();
        }
    }

    public Visibility HideText
    {
        get => _hideText;
        private set
        {
            _hideText = value;
            OnPropertyChanged();
        }
    }

    
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    
    private void InputButton_Click(object sender, RoutedEventArgs e)
    {
        _clickCount++;

        if (!_isCapturing)
        {
            // 奇数次点击：开始捕获
            if (_clickCount % 2 == 1)
            {
                StartCapturing();
            }
        }
        else
        {
            // 偶数次点击：捕获鼠标左键
            if (_clickCount % 2 == 0)
            {
                _mouseButtons.Add(1);
                CompleteCapture();
            }
        }
    }

    private void InputButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturing) return;

        e.Handled = true;

        // 添加任何按键
        _pressedKeys.Add(e.Key);

        // 任何按键都完成捕获，不需要修饰键
        CompleteCapture();

        UpdateCaptureDisplay();
    }

    private void InputButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // 在捕获状态下处理其他鼠标按键（除了左键）
        if (!_isCapturing) return;

        var pointerPoint = e.GetCurrentPoint(InputButton);
        var properties = pointerPoint.Properties;

        // 检测其他鼠标按键（不包括左键，左键通过点击计数处理）
        if (properties.IsRightButtonPressed)
        {
            _mouseButtons.Add(2);
            CompleteCapture();
        }
        else if (properties.IsMiddleButtonPressed)
        {
            _mouseButtons.Add(3);
            CompleteCapture();
        }
        else if (properties.IsXButton1Pressed)
        {
            _mouseButtons.Add(4);
            CompleteCapture();
        }
        else if (properties.IsXButton2Pressed)
        {
            _mouseButtons.Add(5);
            CompleteCapture();
        }
    }

    private void InputButton_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!_isCapturing) return;

        var pointerPoint = e.GetCurrentPoint(InputButton);
        var properties = pointerPoint.Properties;

        if (properties.MouseWheelDelta != 0)
        {
            // 区分滚轮方向：向上为6，向下为7
            var wheelCode = properties.MouseWheelDelta > 0 ? 6 : 7;
            _mouseButtons.Add(wheelCode);
            CompleteCapture();
        }
    }

    private void InputButton_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isCapturing)
        {
            CancelCapture();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        HotkeyKeys = new List<int>();
        OnHotkeyChanged();
    }

    private void StartCapturing()
    {
        _isCapturing = true;
        _pressedKeys.Clear();
        _mouseButtons.Clear();
        ButtonText = "请按下快捷键或鼠标按键";
        ShowPlaceholder = Visibility.Collapsed;
        HideText = Visibility.Collapsed;
        InputButton.Focus(FocusState.Programmatic);
    }

    private void CompleteCapture()
    {
        _isCapturing = false;

        List<int> keys = new();

        // 添加键盘按键
        keys.AddRange(_pressedKeys.OrderBy(GetKeyOrder).Select(k => (int)k));

        // 添加鼠标按键（如果有）
        if (_mouseButtons.Count > 0)
        {
            // 如果捕获到鼠标按键，只保留第一个鼠标按键
            keys.Add(_mouseButtons.First());
        }

        HotkeyKeys = keys;
        _clickCount = 0; // 重置点击计数

        UpdateDisplay();
        OnHotkeyChanged();
    }

    private void CancelCapture()
    {
        _isCapturing = false;
        _clickCount = 0; // 重置点击计数
        UpdateDisplay();
    }

    private void UpdateCaptureDisplay()
    {
        if (_isCapturing)
        {
            List<string> displayParts = new();

            if (_pressedKeys.Count > 0)
            {
                IOrderedEnumerable<VirtualKey> sortedKeys = _pressedKeys.OrderBy(GetKeyOrder);
                displayParts.AddRange(sortedKeys.Select(GetKeyDisplayName));
            }

            if (_mouseButtons.Count > 0)
            {
                displayParts.Add(GetMouseDisplayName(_mouseButtons.First()));
            }

            if (displayParts.Count > 0)
            {
                ButtonText = string.Join(" + ", displayParts);
            }
        }
    }

    private void UpdateDisplay()
    {
        if (HotkeyKeys.Count == 0)
        {
            ButtonText = "";
            HotkeyDisplayText = "";
            ShowPlaceholder = Visibility.Visible;
            HideText = Visibility.Collapsed;
        }
        else
        {
            var displayParts = new List<string>();

            foreach (var keyCode in HotkeyKeys)
            {
                if (IsMouseKeyCode(keyCode))
                {
                    displayParts.Add(GetMouseDisplayName(keyCode));
                }
                else
                {
                    displayParts.Add(GetKeyDisplayName((VirtualKey)keyCode));
                }
            }

            // 排序：键盘按键在前，鼠标按键在后
            var sortedParts = displayParts
                .Select((part, index) => new { Part = part, Index = index, IsMouse = IsMouseKeyCode(HotkeyKeys[index]) })
                .OrderBy(x => x.IsMouse ? 1 : 0)
                .ThenBy(x => x.Index)
                .Select(x => x.Part);

            var displayText = string.Join(" + ", sortedParts);

            if (_isCapturing)
            {
                ButtonText = displayText;
                HotkeyDisplayText = "";
                ShowPlaceholder = Visibility.Collapsed;
                HideText = Visibility.Collapsed;
            }
            else
            {
                ButtonText = "";
                HotkeyDisplayText = displayText;
                ShowPlaceholder = Visibility.Collapsed;
                HideText = Visibility.Visible;
            }
        }

        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (!_isCapturing && (HotkeyKeys.Count == 0))
        {
            ButtonText = "点击设置快捷键";
        }
    }

    private static bool IsModifierKey(VirtualKey key)
    {
        return key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
                    or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
                    or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
                    or VirtualKey.LeftWindows or VirtualKey.RightWindows;
    }

    private static int GetKeyOrder(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.LeftWindows or VirtualKey.RightWindows => 1,
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => 2,
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => 3,
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => 4,
            _ => 5
        };
    }

    private static string GetKeyDisplayName(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.LeftWindows or VirtualKey.RightWindows => "Win",
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => "Ctrl",
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => "Alt",
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => "Shift",
            VirtualKey.Space => "Space",
            VirtualKey.Tab => "Tab",
            VirtualKey.Enter => "Enter",
            VirtualKey.Escape => "Esc",
            VirtualKey.Back => "Backspace",
            VirtualKey.Delete => "Delete",
            VirtualKey.Insert => "Insert",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "Page Up",
            VirtualKey.PageDown => "Page Down",
            VirtualKey.Up => "↑",
            VirtualKey.Down => "↓",
            VirtualKey.Left => "←",
            VirtualKey.Right => "→",
            VirtualKey.F1 => "F1",
            VirtualKey.F2 => "F2",
            VirtualKey.F3 => "F3",
            VirtualKey.F4 => "F4",
            VirtualKey.F5 => "F5",
            VirtualKey.F6 => "F6",
            VirtualKey.F7 => "F7",
            VirtualKey.F8 => "F8",
            VirtualKey.F9 => "F9",
            VirtualKey.F10 => "F10",
            VirtualKey.F11 => "F11",
            VirtualKey.F12 => "F12",
            _ => key.ToString()
        };
    }

    public event EventHandler? HotkeyChanged;

    private void OnHotkeyChanged()
    {
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    // 辅助方法
    private static bool IsMouseKeyCode(int keyCode) => keyCode switch
    {
        >= 1 and <= 7 => true, // 鼠标按键（包括滚轮上下6,7）
        _ => false
    };

    private static string GetMouseDisplayName(int mouseCode) => mouseCode switch
    {
        1 => "MOUSE_LEFT",
        2 => "MOUSE_RIGHT",
        3 => "MOUSE_MIDDLE",
        4 => "MOUSE_X1",
        5 => "MOUSE_X2",
        6 => "WHEEL_UP",
        7 => "WHEEL_DOWN",
        _ => "MOUSE_UNKNOWN"
    };
}