using GalgameManager.Contracts.Services;
using GalgameManager.Enums;

namespace GalgameManager.Helpers;

/// <summary>
/// Stores pending PotatoVN cloud deletions outside the Windows application settings container.
/// The old small-setting representation is migrated on first access.
/// </summary>
public static class PvnPendingDeletionStore
{
    private static readonly SemaphoreSlim GameLock = new(1, 1);
    private static readonly SemaphoreSlim StaffLock = new(1, 1);

    public static Task<List<int>> GetGamesAsync(ILocalSettingsService settings) =>
        ReadAsync(settings, KeyValues.ToDeleteGames, GameLock);

    public static Task AddGameAsync(ILocalSettingsService settings, int id) =>
        AddAsync(settings, KeyValues.ToDeleteGames, id, GameLock);

    public static Task RemoveGameAsync(ILocalSettingsService settings, int id) =>
        RemoveAsync(settings, KeyValues.ToDeleteGames, id, GameLock);

    public static Task ClearGamesAsync(ILocalSettingsService settings) =>
        SaveAsync(settings, KeyValues.ToDeleteGames, [], GameLock);

    public static Task<List<int>> GetStaffAsync(ILocalSettingsService settings) =>
        ReadAsync(settings, KeyValues.ToDeleteStaff, StaffLock);

    public static Task AddStaffAsync(ILocalSettingsService settings, int id) =>
        AddAsync(settings, KeyValues.ToDeleteStaff, id, StaffLock);

    public static Task ClearStaffAsync(ILocalSettingsService settings) =>
        SaveAsync(settings, KeyValues.ToDeleteStaff, [], StaffLock);

    private static async Task<List<int>> ReadAsync(
        ILocalSettingsService settings,
        string key,
        SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        try
        {
            return await ReadAndMigrateUnlockedAsync(settings, key);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task AddAsync(
        ILocalSettingsService settings,
        string key,
        int id,
        SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        try
        {
            List<int> ids = await ReadAndMigrateUnlockedAsync(settings, key);
            if (!ids.Contains(id))
            {
                ids.Add(id);
                await settings.SaveSettingAsync(key, ids, true);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task RemoveAsync(
        ILocalSettingsService settings,
        string key,
        int id,
        SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        try
        {
            List<int> ids = await ReadAndMigrateUnlockedAsync(settings, key);
            if (ids.RemoveAll(item => item == id) > 0)
                await settings.SaveSettingAsync(key, ids, true);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task SaveAsync(
        ILocalSettingsService settings,
        string key,
        List<int> ids,
        SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        try
        {
            await settings.SaveSettingAsync(key, ids.Distinct().ToList(), true);
            await settings.RemoveSettingAsync(key);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<List<int>> ReadAndMigrateUnlockedAsync(
        ILocalSettingsService settings,
        string key)
    {
        List<int>? stored = await settings.ReadSettingAsync<List<int>>(key, true);
        List<int>? legacy = await settings.ReadSettingAsync<List<int>>(key);
        List<int> merged = (stored ?? [])
            .Concat(legacy ?? [])
            .Distinct()
            .ToList();

        if (legacy is not null)
        {
            await settings.SaveSettingAsync(key, merged, true);
            await settings.RemoveSettingAsync(key);
        }

        return merged;
    }
}
