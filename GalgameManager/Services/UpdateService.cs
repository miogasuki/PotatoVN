using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Services;

public class UpdateService : IUpdateService
{
    private readonly bool _firstUpdate;
    private readonly ILocalSettingsService _localSettingsService;

    public event Action<bool>? SettingBadgeEvent;

    public UpdateService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        var last = localSettingsService.ReadSettingAsync<string>(KeyValues.DisplayedUpdateVersion).Result ?? "";
        _firstUpdate = last != RuntimeHelper.GetVersion();
    }

    public async Task<bool> CheckUpdateAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastUpdateCheckDate) is var lastDate 
            && lastDate.Date == DateTime.Now.Date 
            && await _localSettingsService.ReadSettingAsync<bool>(KeyValues.LastUpdateCheckResult) == false)
        {
            return false;
        }
        
        try
        {
            HttpClient client = Utils.GetDefaultHttpClient();
            HttpResponseMessage response = await client.GetAsync(
                "https://raw.gitmirror.com/GoldenPotato137/GalgameManager/main/docs/version");
            var newestVersion = (await response.Content.ReadAsStringAsync())
                .Replace("\n", "").Replace("\r","");
            var result = newestVersion != RuntimeHelper.GetVersion();
            await _localSettingsService.SaveSettingAsync(KeyValues.LastUpdateCheckDate, DateTime.Now.Date);
            await _localSettingsService.SaveSettingAsync(KeyValues.LastUpdateCheckResult, result);
            return result;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task UpdateSettingsBadgeAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<string>(KeyValues.LastNoticeUpdateVersion) !=
            RuntimeHelper.GetVersion() && await CheckUpdateAsync())
            SettingBadgeEvent?.Invoke(true);
        else
            SettingBadgeEvent?.Invoke(false);
    }

    public bool ShouldDisplayUpdateContent() => _firstUpdate;
}