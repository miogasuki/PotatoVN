using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class KeyMappingDialog : ContentDialog
{
    public GalgameSettingViewModel ViewModel { get; }
    
    public KeyMappingDialog(GalgameSettingViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}