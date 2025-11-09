using CommunityToolkit.WinUI;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class GalgameSettingPage : Page
{
    public GalgameSettingViewModel ViewModel { get; private set; } = null!;

    public string SavePositionDescription =>
        string.IsNullOrEmpty(ViewModel?.Gal?.DetectedSavePosition)
            ? "GalgameSettingPage_DetectedSavePosition".GetLocalized()!
            : ViewModel.Gal.DetectedSavePosition;

    public GalgameSettingPage()
    {
        ViewModel = App.GetService<GalgameSettingViewModel>();
        InitializeComponent();

        // 监听DetectedSavePosition变化以更新UI
        if (ViewModel?.Gal != null)
        {
            ViewModel.Gal.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.Gal.DetectedSavePosition))
                {
                    // 通知绑定更新
                    Bindings.Update();
                }
            };
        }
    }
}
