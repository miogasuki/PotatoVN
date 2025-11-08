using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;
using GalgameManager.Views.Control;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.WinUI;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Views.Dialog;

public sealed partial class KeyMappingDialog : ContentDialog
{
    public GalgameSettingViewModel ViewModel { get; }

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

        // 添加PrimaryButtonClick事件处理
        PrimaryButtonClick += OnSaveButtonClick;
    }

    private void RemoveKeyMapping(KeyMapping? mapping)
    {
        if (mapping != null) // 允许删除所有快捷键，包括全局快捷键
        {
            ViewModel.KeyMappings.Remove(mapping);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is KeyMapping mapping)
        {
            RemoveKeyMapping(mapping);
        }
    }

    private async void OnSaveButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 调用ViewModel的保存方法
        await ViewModel.SaveKeyMappingsAsync();
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
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
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