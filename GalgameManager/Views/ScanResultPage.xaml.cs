using GalgameManager.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class ScanResultPage : Page
{
    public ScanResultViewModel ViewModel
    {
        get;
    }

    public ScanResultPage()
    {
        ViewModel = App.GetService<ScanResultViewModel>();
        InitializeComponent();
    }
}
