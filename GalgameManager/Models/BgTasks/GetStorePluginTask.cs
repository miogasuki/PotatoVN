using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Helpers.API.RepoFlow;
using Newtonsoft.Json;

namespace GalgameManager.Models.BgTasks;

public class GetStorePluginTask(ObservableCollection<StorePlugin> pluginList) : QueueTaskBase<StorePlugin>
{
    protected override int MaxRunning() => 5;
    private readonly IRepoFlowApi _api = RepoFlowApi.GetApi();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private readonly HttpClient _httpClient = Utils.GetDefaultHttpClient();

    private const string DocPackageName = "doc";
    private const string PluginPackageName = "plugin";
    private const string InfoFileName = "plugin-info.json";
    private const string PluginFileName = "plugin.pvnplugin.zip";

    protected async override Task InitializeAsync()
    {
        await base.InitializeAsync();
        await UiThreadInvokeHelper.InvokeAsync(pluginList.Clear);
        List<Repository> tmp = await _api.GetRepositoriesAsync(RepoFlowApi.WorkspaceId);
        foreach (Repository rep in tmp)
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                Queue.Enqueue(new StorePlugin
                {
                    RepoName = rep.Name,
                });
            });
    }

    public override string Title => "GetStorePluginTask_Title".GetLocalized();

    protected async override Task ProcessItemAsync(StorePlugin item)
    {
        List<Task> tasks =
        [
            GetPluginInfo(item),
            GetPluginVersionsInfo(item),
        ];
        try
        {
            await Task.WhenAll(tasks);
            if (item.Versions.Count > 0)
                item.ReleaseDate = item.Versions[0].ReleaseDate;
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: $"获取插件{item.RepoName}信息失败", e: e);
            return;
        }
        
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            pluginList.Add(item);
        });
    }

    protected override string ProgressTitle() => "GetStorePluginTask_ProgressTitle";

    protected override string ProgressMsg(StorePlugin item) => string.Empty;

    protected override string ProgressWaitingMsg() => "GetStorePluginTask_ProgressWaitingMsg";

    private async Task GetPluginInfo(StorePlugin plugin)
    {
        Version docVer = (await _api.GetPackageMetaAsync(RepoFlowApi.WorkspaceName, plugin.RepoName, DocPackageName))
            .LatestVersion;
        PluginInfo? info = JsonConvert.DeserializeObject<PluginInfo>(await _httpClient.GetStringAsync(
            RepoFlowApi.GetDownloadUrl(plugin.RepoName, DocPackageName,
                docVer.ToString(), InfoFileName)));
        if (info is null) throw new PvnException("找不到插件信息文件或格式错误");
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            plugin.Name = info.Name;
            plugin.DescriptionShort = info.DescriptionShort;
            plugin.DescriptionDetailed = info.DescriptionDetailed;
            plugin.LogoUrl = RepoFlowApi.GetDownloadUrl(plugin.RepoName, DocPackageName,
                docVer.ToString(), info.IconFileName);
        });
    }

    private async Task GetPluginVersionsInfo(StorePlugin plugin)
    {
        List<PackageDetailVersion> tmp =
            await _api.GetPackageDetailVersionAsync(RepoFlowApi.WorkspaceName, plugin.RepoName, PluginPackageName);
        foreach (PackageDetailVersion version in tmp)
        {
            var downloadUrl = RepoFlowApi.GetDownloadUrl(plugin.RepoName, PluginPackageName,
                version.Version.ToString(), PluginFileName);
            plugin.Versions.Add(new StorePluginVersion
            {
                Version = version.Version,
                DownloadUrl = downloadUrl,
                ReleaseDate = version.CreatedAt,
            });
            plugin.Versions.Sort((a, b) => b.Version.CompareTo(a.Version));
        }
    }

    private class PluginInfo
    {
        [JsonProperty("name")] public string Name = string.Empty;
        [JsonProperty("description_short")] public string DescriptionShort = string.Empty;
        [JsonProperty("description_detailed")] public string DescriptionDetailed = string.Empty;
        [JsonProperty("icon")] public string IconFileName = string.Empty;
    }
}