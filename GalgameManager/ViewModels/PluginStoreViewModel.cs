using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class PluginStoreViewModel(IInfoService infoService, IBgTaskService bgTaskService, IPluginService pluginService)
    : ObservableRecipient, INavigationAware
{
    public ObservableCollection<StorePlugin> Plugins { get; } = new();

    public void OnNavigatedTo(object parameter)
    {
        bgTaskService.AddBgTask(new GetStorePluginTask(Plugins));
    }

    public void OnNavigatedFrom() {}

    [RelayCommand]
    private async Task ItemClickAsync(StorePlugin? clickedItem)
    {
        try
        {
            if (clickedItem == null) return;
            // infoService.Info(InfoBarSeverity.Success, clickedItem.Name);
            StorePluginDialog dialog = new(clickedItem, pluginService.PluginOffloadInProgress);
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                StorePluginVersion version = dialog.SelectedVersion;
                await bgTaskService.AddBgTask(new InstallStorePluginTask(clickedItem, version));
            }
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
    }
}
