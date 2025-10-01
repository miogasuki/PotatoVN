using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Contracts.PluginUi;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class PluginViewModel(IPluginService pluginService, IInfoService infoService)
    : ObservableRecipient, INavigationAware
{
    public ObservableCollection<PluginSettingViewModel> Plugins = [];

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            ObservableCollection<PluginX> tmp = await pluginService.GetAllPluginsAsync();
            foreach (PluginX plugin in tmp)
                Plugins.Add(new PluginSettingViewModel(plugin));
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
    }

    public void OnNavigatedFrom() { }

    [RelayCommand]
    private async Task AddPlugin()
    {
        try
        {
            FolderPicker folderPicker = new();
            folderPicker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());
            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
            if (folder is null) return;
            await pluginService.AddPluginAsync(folder.Path);
        }
        catch (Exception e)
        {
            infoService.Info(InfoBarSeverity.Error, "PluginPage_Add_Failed".GetLocalized(),
                e is PvnException pvnE ? pvnE.FullMsg : e.ToString());
        }
    }
}

public partial class PluginSettingViewModel (PluginX plugin) : ObservableRecipient
{
    public PluginX Plugin { get; } = plugin;
    [ObservableProperty] private FrameworkElement? _ui;
    [ObservableProperty] private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value) return;
        if (Ui != null) return;
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (Plugin.Plugin is not IPluginSetting setting) return;
        try
        {
            FrameworkElement pluginUi = setting.CreateSettingUi();
            Ui = pluginUi;
        }
        catch (Exception ex)
        {
            App.GetService<IPluginService>().ThrowPluginExceptionEvent(Plugin, ex, 
                "PluginSettingViewModel_CreateUiFailed".GetLocalized());
        }
    }
}
