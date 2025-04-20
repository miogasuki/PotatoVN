using GalgameManager.Contracts.BgTasks;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;

namespace GalgameManager.Models.BgTasks;

public class GetHeaderFromRssTask : QueueTaskBase<Galgame>, IGameProcessQueue
{
    private static readonly IGalgameCollectionService GameService = App.GetService<IGalgameCollectionService>();
    private readonly VndbPhraser _vndbParser = (GameService.PhraserList[(int)RssType.Vndb] as VndbPhraser)!;

    public void AddGalgame(Galgame? game)
    {
        if (game is null) return;
        Queue.Enqueue(game);
        UpdateProgressMsg();
    }
    
    public override string Title => "GetHeaderFromRssTask_Title".GetLocalized();
    
    protected async override Task ProcessItemAsync(Galgame item)
    {
        item.AutoFetchStatus.HeaderImage = true;
        await GameService.SaveGalgameAsync(item);
        var url = await _vndbParser.GetGalHeaderAsync(item);
        if (url is null) return;
        item.HeaderImageUrl = url;
        var targetPath = Path.Combine((await FileHelper.GetFolderAsync(FileHelper.FolderType.Images)).Path,
            $"{item.Name.Value}_Header.png".RemoveInvalidChars());
        var rawImage = await DownloadHelper.DownloadAndSaveImageWithDiffThread(url,
            fileNameWithoutExtension: $"{item.Name.Value ?? string.Empty}_tmp");
        if (rawImage is null) return;
        DownloadHelper.ProcessImage(rawImage, targetPath, true);
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            item.HeaderImagePath.Value = targetPath;
            item.RaisePropertyChanged(nameof(item.HeaderImagePath));
        });
        await GameService.SaveGalgameAsync(item);
        if (File.Exists(rawImage)) File.Delete(rawImage);
    }

    protected override string ProgressTitle() => "GetHeaderFromRssTask_Progress_Title";

    protected override string ProgressMsg(Galgame item) => item.Name.Value ?? string.Empty;

    protected override string ProgressWaitingMsg() => "GetHeaderFromRssTask_Progress_Waiting";
}