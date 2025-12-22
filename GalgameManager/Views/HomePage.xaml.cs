using System.ComponentModel;
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
        ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void MainGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel.IsBatchMode) return;
        if (e.ClickedItem is Galgame galgame)
        {
            ViewModel.ItemClickCommand.Execute(galgame);
        }
    }

    private void MainGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.MainGridViewSelectionChangedCommand.Execute(e);
        UpdateSelectionOpacity(e);
    }

    private void MainGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is GridViewItem container)
            ApplyBatchOpacity(container);
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.IsBatchMode)) return;
        if (!ViewModel.IsBatchMode)
            GridView.SelectedItems.Clear();
        UpdateAllItemOpacity();
    }

    private void UpdateSelectionOpacity(SelectionChangedEventArgs e)
    {
        foreach (var item in e.AddedItems)
            UpdateContainerOpacity(item);
        foreach (var item in e.RemovedItems)
            UpdateContainerOpacity(item);
    }

    private void UpdateAllItemOpacity()
    {
        foreach (var item in GridView.Items)
            UpdateContainerOpacity(item);
    }

    private void UpdateContainerOpacity(object item)
    {
        if (GridView.ContainerFromItem(item) is GridViewItem container)
            ApplyBatchOpacity(container);
    }

    private void ApplyBatchOpacity(GridViewItem container)
    {
        if (!ViewModel.IsBatchMode)
        {
            container.Opacity = 1;
            return;
        }

        container.Opacity = container.IsSelected ? 1 : 0.4;
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
