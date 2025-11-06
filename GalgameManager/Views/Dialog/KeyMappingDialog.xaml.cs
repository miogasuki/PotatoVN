using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Dialog;

public sealed partial class KeyMappingDialog : ContentDialog
{
    public GalgameSettingViewModel ViewModel { get; }

    // Localization properties
    public string KeyMappingDialog_Title => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Title") ?? string.Empty;
    public string KeyMappingDialog_Button_Close => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Button_Close") ?? string.Empty;
    public string KeyMappingDialog_Header_Description => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Header_Description") ?? string.Empty;
    public string KeyMappingDialog_Header_FromKey => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Header_FromKey") ?? string.Empty;
    public string KeyMappingDialog_Header_ToKey => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Header_ToKey") ?? string.Empty;
    public string KeyMappingDialog_Header_Enabled => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Header_Enabled") ?? string.Empty;
    public string KeyMappingDialog_Header_Delete => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Header_Delete") ?? string.Empty;
    public string KeyMappingDialog_Placeholder_Description => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Placeholder_Description") ?? string.Empty;
    public string KeyMappingDialog_Button_Delete_ToolTip => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Button_Delete_ToolTip") ?? string.Empty;
    public string KeyMappingDialog_Button_AddMapping => GalgameManager.Helpers.ResourceExtensions.GetLocalized("KeyMappingDialog_Button_AddMapping") ?? string.Empty;

    public KeyMappingDialog(GalgameSettingViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
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