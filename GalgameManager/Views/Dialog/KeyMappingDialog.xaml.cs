using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;
using GalgameManager.Views.Control;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.WinUI;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace GalgameManager.Views.Dialog;

public sealed partial class KeyMappingDialog : ContentDialog
{
    public GalgameSettingViewModel ViewModel { get; }
    public ObservableCollection<KeyMapping> DialogKeyMappings { get; private set; } = null!;
    private readonly ObservableCollection<KeyMapping> _originalKeyMappings;

    public KeyMappingDialog(GalgameSettingViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Title = "KeyMappingDialog_Title".GetLocalized();
        PrimaryButtonText = "KeyMappingDialog_Button_Save".GetLocalized();
        CloseButtonText = "KeyMappingDialog_Button_Close".GetLocalized();

        // 创建原始数据的深拷贝，完全不修改 ViewModel 的数据
        _originalKeyMappings = new ObservableCollection<KeyMapping>(ViewModel.KeyMappings);
        DialogKeyMappings = CreateDeepCopy(_originalKeyMappings);

        // 添加PrimaryButtonClick事件处理
        PrimaryButtonClick += OnSaveButtonClick;
    }

    private ObservableCollection<KeyMapping> CreateDeepCopy(ObservableCollection<KeyMapping> source)
    {
        var copy = new ObservableCollection<KeyMapping>();
        foreach (var mapping in source)
        {
            copy.Add(new KeyMapping
            {
                Remark = mapping.Remark,
                From = mapping.From?.ToList() ?? [],
                To = mapping.To?.ToList() ?? [],
                IsEnabled = mapping.IsEnabled,
                IsGlobal = mapping.IsGlobal
            });
        }
        return copy;
    }

    private void RemoveKeyMapping(KeyMapping? mapping)
    {
        if (mapping != null) // 允许删除所有快捷键，包括全局快捷键
        {
            DialogKeyMappings.Remove(mapping);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is KeyMapping mapping)
        {
            RemoveKeyMapping(mapping);
        }
    }

    private void OnSaveButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 只有在用户点击保存时，才将对话框中的数据应用到 ViewModel
        ViewModel.KeyMappings = new ObservableCollection<KeyMapping>(DialogKeyMappings);
    }

    private void AddKeyMapping()
    {
        DialogKeyMappings.Add(new KeyMapping { IsGlobal = false });
    }

    private void AddKeyMappingButton_Click(object sender, RoutedEventArgs e)
    {
        AddKeyMapping();
    }

    private void InputModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            var newMode = toggleSwitch.IsOn ? InputMode.Dropdown : InputMode.Capture;
            UpdateAllInputBoxes(newMode);
        }
    }

    private void UpdateAllInputBoxes(InputMode mode)
    {
        // 遍历所有FlexibleHotkeyInputBox并更新它们的模式
        var inputBoxes = FindVisualChildren<FlexibleHotkeyInputBox>(this);
        foreach (var inputBox in inputBoxes)
        {
            inputBox.InputMode = mode;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
    {
        if (depObj != null)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject? child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                {
                    yield return (T)child;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
}