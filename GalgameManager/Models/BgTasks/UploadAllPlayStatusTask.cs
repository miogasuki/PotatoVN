using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class UploadAllPlayStatusTask(bool uploadBangumi, bool uploadVndb) : QueueTaskBase<Galgame>
{
    private static readonly IGalgameCollectionService GameService = App.GetService<IGalgameCollectionService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();

    public void AddGalgame(Galgame? game)
    {
        if (game is null) return;
        Queue.Enqueue(game);
        UpdateProgressMsg();
    }

    public override string Title => "UploadAllPlayStatusTask_Title".GetLocalized();

    protected async override Task InitializeAsync()
    {
        // Enqueue all games that have play status != None
        foreach (Galgame g in GameService.Galgames)
        {
            if (g.PlayType != PlayType.None)
                Queue.Enqueue(g);
        }
        UpdateProgressMsg();
        await Task.CompletedTask;
    }

    protected async override Task ProcessItemAsync(Galgame item)
    {
        try
        {
            if (uploadBangumi)
            {
                try
                {
                    await GameService.UploadPlayStatusAsync(item, RssType.Bangumi);
                }
                catch (Exception e)
                {
                    _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                        "UploadAllPlayStatusTask_UploadBgmFailed".GetLocalized(item.Name.Value ?? string.Empty), e);
                }
            }

            if (uploadVndb)
            {
                try
                {
                    await GameService.UploadPlayStatusAsync(item, RssType.Vndb);
                }
                catch (Exception e)
                {
                    _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                        "UploadAllPlayStatusTask_UploadVndbFailed".GetLocalized(item.Name.Value ?? string.Empty), e);
                }
            }
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                "UploadAllPlayStatusTask_GameFailed".GetLocalized(item.Name.Value ?? string.Empty), e);
        }
    }

    protected override string ProgressTitle() => "UploadAllPlayStatusTask_Progress_Title";

    protected override string ProgressMsg(Galgame item) => item.Name.Value ?? string.Empty;

    protected override string ProgressWaitingMsg() => "UploadAllPlayStatusTask_Progress_Waiting";
}