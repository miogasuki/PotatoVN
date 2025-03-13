using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using PotatoVN.Client.Model;
using PlayType = GalgameManager.Enums.PlayType;

namespace GalgameManager.Models.BgTasks;

public class PvnSyncTask : BgTaskBase
{
    public string Result { get; set; } = string.Empty;
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        GalgameCollectionService gameService =
            (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!;
        IInfoService infoService = App.GetService<IInfoService>();
        IPvnService pvnService = App.GetService<IPvnService>();
        ILocalSettingsService settingsService = App.GetService<ILocalSettingsService>();

        if (settingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount).Result is null)
            throw new PvnException("PvnSyncTask_Error_NotLogin".GetLocalized());

        try
        {
            ChangeProgress(0, 1, "PvnSyncTask_GettingModifiedTimestamp".GetLocalized());
            var lastSync = await settingsService.ReadSettingAsync<long>(KeyValues.PvnSyncTimestamp);
            var latest = await pvnService.GetLastGalChangedTimeStampAsync();
            if (lastSync < latest)
                await PullUpdates(pvnService, gameService, settingsService, latest);
        }
        catch (Exception e)
        {
            var failedReason = "PvnSyncTask_Error".GetLocalized();
            if (e is HttpRequestException)
                failedReason = "PvnSyncTask_Error_Network".GetLocalized();
            ChangeProgress(-1, 1, failedReason);
            throw new PvnException($"{failedReason}\n{e}");
        }

        await CommitChanges(gameService, pvnService, infoService, settingsService);


        var notify = true;
        if (Result.IsNullOrEmpty())
        {
            Result = "PvnSyncTask_NoChange".GetLocalized();
            if (!await App.GetService<ILocalSettingsService>()
                    .ReadSettingAsync<bool>(KeyValues.EventPvnSyncEmptyNotify))
                notify = false;
        }

        if (Result.EndsWith('\n')) Result = Result[..^1];
        ChangeProgress(1, 1, Result, notify);
    }

    public override string Title { get; } = "PvnSyncTask_Title".GetLocalized();

    public override bool OnSearch(string key) => true;

    [SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
    private async Task PullUpdates(IPvnService pvnService, GalgameCollectionService gameService,
        ILocalSettingsService settingsService, long latest)
    {
        ChangeProgress(0, 1, "PvnSyncTask_GettingModifiedList".GetLocalized());
        List<GalgameDto> changedGalgames = await pvnService.GetChangedGalgamesAsync();
        List<int> deletedGalgames = await pvnService.GetDeletedGalgamesAsync();
        var isRetrying = false;
        for (var index = 0; index < changedGalgames.Count; index++)
        {
            try
            {
                GalgameDto dto = changedGalgames[index];
                Galgame? game = gameService.GetGalgameFromId(dto.Id.ToString(), RssType.PotatoVn);
                await UiThreadInvokeHelper.InvokeAsync(async Task () =>
                {
                    game ??= gameService.GetGalgameFromUid(new GalgameUid
                    {
                        BangumiId = dto.BgmId,
                        VndbId = dto.VndbId,
                        Name = dto.Name ?? string.Empty,
                        CnName = dto.CnName,
                    });
                    
                    if (game is null) //同步进来的游戏
                    {
                        game = new Galgame();
                        gameService.AddVirtualGalgame(game);
                        Result += "PvnSyncTask_Pull_Added".GetLocalized(dto.Name ?? string.Empty, dto.Id) + "\n";
                    }
                    else
                        Result += "PvnSyncTask_Pull_Updated".GetLocalized(dto.Name ?? string.Empty, dto.Id) + "\n";

                    ChangeProgress(index, changedGalgames.Count,
                        "PvnSyncTask_Downloading".GetLocalized(dto.Name ?? string.Empty, dto.Id));

                    game.Ids[(int)RssType.PotatoVn] = dto.Id.ToString();
                    game.Ids[(int)RssType.Bangumi] = dto.BgmId ?? game.Ids[(int)RssType.Bangumi];
                    game.Ids[(int)RssType.Vndb] = dto.VndbId ?? game.Ids[(int)RssType.Vndb];
                    game.UpdateMixedId();
                    game.Name = dto.Name ?? game.Name.Value ?? string.Empty;
                    game.CnName = dto.CnName ?? game.CnName;
                    game.Description = dto.Description ?? game.Description.Value ?? string.Empty;
                    game.Developer = dto.Developer ?? game.Developer.Value ?? string.Empty;
                    game.ExpectedPlayTime = dto.ExpectedPlayTime ?? game.ExpectedPlayTime.Value ?? string.Empty;
                    game.Rating = dto.Rating;
                    game.ReleaseDate = dto.ReleasedDateTimeStamp.ToDateTime().ToLocalTime();
                    if (dto.ImageUrl is not null)
                        game.ImagePath = await DownloadHelper.DownloadAndSaveImageAsync(dto.ImageUrl, 0,
                            $"pvn_{dto.Id}") ?? game.ImagePath.Value ?? Galgame.DefaultImagePath;
                    if (dto.Tags is not null)
                    {
                        game.Tags.Value?.Clear();
                        dto.Tags.ForEach(tag => game.Tags.Value?.Add(tag));
                    }
                    if (dto.PlayTime is not null)
                    {
                        Dictionary<string, int> playTime = new();
                        foreach (PlayLogDto time in dto.PlayTime)
                            playTime[time.DateTimeStamp.ToDateTime().ToLocalTime().ToStringDefault()] = time.Minute;
                        game.MergeTime(new Galgame { PlayedTime = playTime });
                    }
                    game.PlayType = (PlayType)(int)(dto.PlayType ?? 0);
                    game.Comment = dto.Comment ?? game.Comment;
                    game.MyRate = dto.MyRate;
                    game.PrivateComment = dto.PrivateComment;
                    game.PvnUpdate = false;

                    await gameService.SaveGalgameAsync(game);
                });
            }
            catch (COMException) //奇怪的UI异常，重试
            {
                if (isRetrying) throw;
                await Task.Delay(200);
                index--;
                isRetrying = true;
                continue;
            }
            isRetrying = false;
        }

        foreach (long id in deletedGalgames)
        {
            Galgame? game = gameService.GetGalgameFromId(id.ToString(), RssType.PotatoVn);
            if (game is null) continue;
            Result += "PvnSyncTask_Pull_Deleted".GetLocalized(game.Name.Value ?? string.Empty, id) + "\n";
            await gameService.RemoveGalgame(game);
        }

        await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, latest);
    }

    private async Task CommitChanges(GalgameCollectionService gameService, IPvnService pvnService,
        IInfoService infoService,
        ILocalSettingsService settingsService)
    {
        List<int> toDelete = await settingsService.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames) ?? new();
        List<int> toDeleteCopy = new(toDelete);
        for (var index = 0; index < toDeleteCopy.Count; index++)
        {
            var id = toDeleteCopy[index];
            try
            {
                ChangeProgress(index, toDeleteCopy.Count, "PvnSyncTask_Deleting".GetLocalized(id));
                await pvnService.DeleteInternal(id);
                toDelete.Remove(id);
                Result += "PvnSyncTask_Commit_Delete".GetLocalized(id) + "\n";
            }
            catch (Exception e)
            {
                if (e is InvalidOperationException) toDelete.Remove(id); //游戏不属于该用户或游戏不存在
                // 否则为网络异常等因素，不应该移出删除列表，等待下次同步重试
                else
                    infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning,
                        "PvnSyncTask_Error_Delete".GetLocalized(id), e);
            }
            finally
            {
                await settingsService.SaveSettingAsync(KeyValues.ToDeleteGames, toDelete);
            }
        }

        List<Galgame> toUpdate;
        HashSet<Galgame> ignore = new();
        do
        {
            toUpdate = gameService.Galgames.Where(g => g.PvnUpdate).ToList();
            toUpdate.RemoveAll(ignore.Contains);
            for (var index = 0; index < toUpdate.Count; index++)
            {
                Galgame game = toUpdate[index];
                try
                {
                    PvnUploadProperties uploadProperties = game.PvnUploadProperties;
                    ChangeProgress(index, toUpdate.Count,
                        "PvnSyncTask_Uploading".GetLocalized(game.Name.Value!));
                    var id = await pvnService.UploadInternal(game);
                    game.Ids[(int)RssType.PotatoVn] = id.ToString();
                    await gameService.SaveGalgameAsync(game);
                    await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, DateTime.Now.ToUnixTime());
                    Result += "PvnSyncTask_Commit".GetLocalized(game.Name.Value!, id, uploadProperties.ToString()) + "\n";
                }
                catch (Exception e)
                {
                    infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning, "PvnSyncTask_Error_Upload".GetLocalized(), e);
                    ignore.Add(game);
                }
            }
        } while (toUpdate.Count > 0);

        try
        {
            //把服务器上的最新时间戳保存到本地（不使用本地时间戳是为了防止本地时间不准）
            var serverLatestChangedTimestamp = await pvnService.GetLastGalChangedTimeStampAsync();
            await settingsService.SaveSettingAsync(KeyValues.PvnSyncTimestamp, serverLatestChangedTimestamp);
        }
        catch (Exception e)
        {
            infoService.Event(EventType.PvnSyncEvent, InfoBarSeverity.Warning,
                "PvnSyncTask_Error_Upload_SaveSyncTime", e);
        }
    }
}