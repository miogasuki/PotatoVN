using System.Collections.Concurrent;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;

namespace GalgameManager.Models.BgTasks;

public class DownloadCategoryImageTask : QueueTaskBase<Category>
{
    private static readonly IGalgameCollectionService GameService = App.GetService<IGalgameCollectionService>();
    private static readonly ICategoryService CategoryService = App.GetService<ICategoryService>();
    private static readonly IInfoService InfoService = App.GetService<IInfoService>();

    private readonly ConcurrentDictionary<Guid, byte> _queuedCategoryIds = new();
    private readonly BgmPhraser _bgmPhraser = (BgmPhraser)GameService.PhraserList[(int)RssType.Bangumi];

    public override string Title => "DownloadCategoryImageTask_Title".GetLocalized();

    public void AddCategory(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Name)) return;
        if (_queuedCategoryIds.TryAdd(category.Id, 0) == false) return;

        Queue.Enqueue(category);
        UpdateProgressMsg();
    }

    protected override int MaxRunning() => 1;

    protected override Task RecoverFromJsonInternal()
    {
        foreach (Category category in Queue)
            _queuedCategoryIds.TryAdd(category.Id, 0);
        return Task.CompletedTask;
    }

    protected async override Task ProcessItemAsync(Category item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item.Name)) return;
            if (CategoryService.GetCategory(item.Id) is null) return;

            var imageUrl = await _bgmPhraser.GetDeveloperImageUrlAsync(item.Name);
            if (imageUrl is null) return;

            var imagePath = await DownloadHelper.DownloadAndSaveImageWithDiffThread(imageUrl);
            if (imagePath is null) return;
            if (CategoryService.GetCategory(item.Id) is null) return;

            await UiThreadInvokeHelper.InvokeAsync(() => item.ImagePath = imagePath);
            if (CategoryService.GetCategory(item.Id) is not null)
                CategoryService.Save(category: item);
        }
        catch (Exception e)
        {
            InfoService.DeveloperEvent(msg: $"failed to download category image: {item.Name}", e: e);
        }
        finally
        {
            _queuedCategoryIds.TryRemove(item.Id, out _);
        }
    }

    protected override string ProgressTitle() => "DownloadCategoryImageTask_Progress";

    protected override string ProgressMsg(Category item) => $"{item.Name} ";

    protected override string ProgressWaitingMsg() => "DownloadCategoryImageTask_Progress_Waiting";
}
