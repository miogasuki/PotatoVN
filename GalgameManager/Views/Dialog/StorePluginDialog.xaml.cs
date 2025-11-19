using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

// treeview有bug，不能使用它默认的多选（会莫名奇妙地越界），因此我们自己实现选择功能
public sealed partial class StorePluginDialog
{
    public StorePluginVersion SelectedVersion =>
        VersionsView.SelectedItem as StorePluginVersion ?? _storePlugin.Versions[0];

    private readonly StorePlugin _storePlugin;
    private readonly bool _pluginOffloadInProgress;

    public StorePluginDialog(StorePlugin plugin, bool pluginOffloadInProgress)
    {
        _storePlugin = plugin;
        _pluginOffloadInProgress = pluginOffloadInProgress;
        XamlRoot = App.MainWindow!.Content!.XamlRoot;
        InitializeComponent();
        Title = plugin.Name;

        DefaultButton = ContentDialogButton.Primary;
        SecondaryButtonText = "Cancel".GetLocalized();
        Update();

        VersionsView.SelectionChanged += (_, _) => Update();
    }

    private void Update()
    {
        if (_pluginOffloadInProgress)
        {
            PrimaryButtonText = "StorePluginDialog_WaitingOffload".GetLocalized();
            IsPrimaryButtonEnabled = false;
        }
        else
        {
            PrimaryButtonText = "StorePluginDialog_DownloadLatest".GetLocalized();
            if (VersionsView.SelectedItem is StorePluginVersion selected)
                PrimaryButtonText = "StorePluginDialog_DownloadSelected".GetLocalized(selected.Version);
            IsPrimaryButtonEnabled = _storePlugin.Versions.Count > 0;
        }
    }
}