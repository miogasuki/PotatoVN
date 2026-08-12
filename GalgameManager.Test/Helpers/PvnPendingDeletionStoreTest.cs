using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class PvnPendingDeletionStoreTest : ServiceTestBase
{
    [Test]
    public async Task GetGamesAsync_MigratesAndDeduplicatesLegacyQueue()
    {
        await Settings.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int> { 1, 2, 2 });
        await Settings.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int> { 2, 3 }, isLarge: true);

        List<int> result = await PvnPendingDeletionStore.GetGamesAsync(Settings);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames).Result, Is.Null);
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames, true).Result,
                Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(Settings.ImmediatelySavedLargeSettingKeys, Does.Contain(KeyValues.ToDeleteGames));
        });
    }

    [Test]
    public async Task RemoveGameAsync_PersistsNewerLargeOnlyIdsBeforeRemovingLegacyQueue()
    {
        await Settings.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int> { 1 });
        await Settings.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int> { 1, 2 }, isLarge: true);

        await PvnPendingDeletionStore.RemoveGameAsync(Settings, 1);

        Assert.Multiple(() =>
        {
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames).Result, Is.Null);
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames, true).Result,
                Is.EqualTo(new[] { 2 }));
            Assert.That(Settings.ImmediatelySavedLargeSettingKeys, Does.Contain(KeyValues.ToDeleteGames));
        });
    }

    [Test]
    public async Task AddGameAsync_DoesNotDuplicateQueuedId()
    {
        await PvnPendingDeletionStore.AddGameAsync(Settings, 42);
        await PvnPendingDeletionStore.AddGameAsync(Settings, 42);

        Assert.That(await PvnPendingDeletionStore.GetGamesAsync(Settings), Is.EqualTo(new[] { 42 }));
    }

    [Test]
    public async Task AddGameAsync_ConcurrentDistinctIdsKeepsEveryIdOnce()
    {
        const int count = 100;
        Settings.LargeSettingOperationDelay = TimeSpan.FromMilliseconds(1);

        await Task.WhenAll(Enumerable.Range(1, count)
            .Select(id => PvnPendingDeletionStore.AddGameAsync(Settings, id)));

        List<int> result = await PvnPendingDeletionStore.GetGamesAsync(Settings);
        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(count));
            Assert.That(result, Is.EquivalentTo(Enumerable.Range(1, count)));
            Assert.That(result.Distinct().ToList(), Has.Count.EqualTo(count));
        });
    }

    [Test]
    public async Task ClearGamesAsync_RemovesLargeAndLegacyQueues()
    {
        await Settings.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int> { 1 });
        await Settings.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int> { 2 }, isLarge: true);

        await PvnPendingDeletionStore.ClearGamesAsync(Settings);

        Assert.Multiple(() =>
        {
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames).Result, Is.Null);
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames, true).Result, Is.Empty);
        });
    }
}
