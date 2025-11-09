using CommunityToolkit.WinUI;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class GalgameSettingPage : Page
{
    public GalgameSettingViewModel ViewModel { get; private set; } = null!;

    public GalgameSettingPage()
    {
        ViewModel = App.GetService<GalgameSettingViewModel>();
        InitializeComponent();
    }
}
