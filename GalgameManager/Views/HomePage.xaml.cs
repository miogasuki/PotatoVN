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

    private void MainGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Galgame galgame)
        {
            ViewModel.ItemClickCommand.Execute(galgame);
        }
    }

    private void MainGridView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        try
        {
            ViewModel.EnterCustomSortMode();
        }
        catch (Exception) { /* ignore */ }
    }
    private void MainGridView_DragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
    {
        try
        {
            ViewModel.SaveCustomSortOrder();
        }
        catch (Exception) { /* ignore */ }
    }
}
