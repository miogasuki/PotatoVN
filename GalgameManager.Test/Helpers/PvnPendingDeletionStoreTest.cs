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
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames).Result,
                Is.EquivalentTo(new[] { 1, 2, 2 }));
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames, true).Result,
                Is.EquivalentTo(new[] { 1, 2, 3 }));
        });
    }

    [Test]
    public async Task GetGamesAsync_LegacyQueueSurvivesUntilEachDeletionIsAcknowledged()
    {
        await Settings.SaveSettingAsync(KeyValues.ToDeleteGames, new List<int> { 1, 2 });

        Assert.That(await PvnPendingDeletionStore.GetGamesAsync(Settings), Is.EquivalentTo(new[] { 1, 2 }));

        // Simulate an abnormal exit before the queued large-setting write reaches disk.
        await Settings.RemoveSettingAsync(KeyValues.ToDeleteGames, true);
        Assert.That(await PvnPendingDeletionStore.GetGamesAsync(Settings), Is.EquivalentTo(new[] { 1, 2 }));

        await PvnPendingDeletionStore.RemoveGameAsync(Settings, 1);

        Assert.Multiple(() =>
        {
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames).Result,
                Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames, true).Result,
                Is.EquivalentTo(new[] { 2 }));
        });

        await PvnPendingDeletionStore.RemoveGameAsync(Settings, 2);

        Assert.Multiple(() =>
        {
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames).Result, Is.Null);
            Assert.That(Settings.ReadSettingAsync<List<int>>(KeyValues.ToDeleteGames, true).Result, Is.Empty);
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
