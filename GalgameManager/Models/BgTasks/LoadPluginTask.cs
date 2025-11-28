using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using LiteDB;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class LoadPluginTask : BgTaskBase
{
    private readonly IPluginService _pluginService = App.GetService<IPluginService>();
    private readonly ILocalSettingsService _settingService = App.GetService<ILocalSettingsService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private DirectoryInfo _pluginDir = null!;
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal() => Task.Run(async () =>
    {
        _pluginDir = _pluginService.PluginDir;
        ILiteCollection<PluginX> db = _settingService.Database.GetCollection<PluginX>("plugin");
        ILiteCollection<PluginService.PluginData> dataDb = _settingService.Database.GetCollection<PluginService.PluginData>("plugin_data");
        List<string> pluginPaths = await _settingService.ReadSettingAsync<List<string>>(KeyValues.PluginPaths) ?? [];
        List<PluginX> plugins = db.FindAll().ToList();
        for (var i = 0; i < plugins.Count; i++)
        {
            PluginX plugin = plugins[i];
            if (plugin.ToDelete)
            {
                DeletePlugin(plugin);
                continue;
            }
            
            ChangeProgress(i, pluginPaths.Count, "LoadPluginTask_Loading".GetLocalized(plugin.Info.Name));
            try
            {
                await _pluginService.LoadPluginAsync(plugin, plugin.Enable);
            }
            catch (Exception e)
            {
                var msg = "LoadPluginTask_ErrorLoading_Msg".GetLocalized(e is PvnException ex ? ex.FullMsg : e.ToString());
                _infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
                    "LoadPluginTask_ErrorLoading".GetLocalized(plugin.Info.Name), msg: msg);
            }
        }
        ChangeProgress(1, 1, string.Empty, false);
        return;

        void DeletePlugin(PluginX plugin)
        {
            try
            {
                if (!IsDevPlugin(plugin) && Directory.Exists(plugin.Path)) 
                    Directory.Delete(plugin.Path, true);
                if (plugin.ToDeleteData) dataDb.Delete(plugin.Id);
                db.Delete(plugin.Id);
            }
            catch (Exception)
            {
                _infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
                    "LoadPluginTask_ErrorLoading".GetLocalized(plugin.Info.Name),
                    msg: "LoadPluginTask_ErrorDeleting_Msg".GetLocalized(plugin.Path));
            }
        }
    });

    public override string Title => "LoadPluginTask_Title".GetLocalized();
    
    private bool IsDevPlugin(PluginX plugin) => !Utils.IsPathContained(_pluginDir.FullName, plugin.Path);
}