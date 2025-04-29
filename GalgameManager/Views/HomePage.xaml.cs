using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;

namespace GalgameManager.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        ViewModel = App.GetService<HomeViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
