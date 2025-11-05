using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Dialog;

public sealed partial class KeyMappingDialog : ContentDialog
{
    public GalgameSettingViewModel ViewModel { get; }

    public KeyMappingDialog(GalgameSettingViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // 设置对话框默认大小
        this.MinWidth = 600;
        this.MinHeight = 300;
    }

    private void RemoveKeyMapping(KeyMapping? mapping)
    {
        if (mapping != null && !mapping.IsGlobal) // 只允许删除非全局快捷键
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
}