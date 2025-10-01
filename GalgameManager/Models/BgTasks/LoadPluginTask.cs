using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using LiteDB;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class LoadPluginTask : BgTaskBase
{
    private readonly IPluginService _pluginService = App.GetService<IPluginService>();
    private readonly ILocalSettingsService _settingService = App.GetService<ILocalSettingsService>();
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal() => Task.Run(async () =>
    {
        ILiteCollection<PluginX> db = _settingService.Database.GetCollection<PluginX>("plugin");
        List<string> pluginPaths = await _settingService.ReadSettingAsync<List<string>>(KeyValues.PluginPaths) ?? [];
        List<PluginX> plugins = db.FindAll().ToList();
        for (var i = 0; i < plugins.Count; i++)
        {
            PluginX plugin = plugins[i];
            ChangeProgress(i, pluginPaths.Count, "LoadPluginTask_Loading".GetLocalized(plugin.Info.Name));
            try
            {
                await _pluginService.LoadPluginAsync(plugin, plugin.Enable);
            }
            catch (Exception e)
            {
                var msg = "LoadPluginTask_ErrorLoading_Msg".GetLocalized(e is PvnException ex ? ex.FullMsg : e.ToString());
                App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Warning,
                    "LoadPluginTask_ErrorLoading".GetLocalized(plugin.Info.Name), msg: msg);
            }
        }
        ChangeProgress(1, 1, string.Empty, false);
    });

    public override string Title => "LoadPluginTask_Title".GetLocalized();
}