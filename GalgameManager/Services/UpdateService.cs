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
        try
        {
            HttpClient client = Utils.GetDefaultHttpClient();
            HttpResponseMessage response = await client.GetAsync(
                "https://potatovn.net/raw/version");
            var versionString = (await response.Content.ReadAsStringAsync())
                            .Replace("\n", "").Replace("\r","");
            
            // 分割版本号，获取正式版和测试版版本
            string[] versions = versionString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (versions.Length < 2)
            {
                // 格式不符合预期，无法判断
                return false;
            }
            
            Version stableVersion = Version.Parse(versions[0].Trim());
            Version betaVersion = Version.Parse(versions[1].Trim());
            Version currentVersion = Version.Parse(RuntimeHelper.GetVersion());
            
            // 判断当前版本是否需要更新
            bool needsUpdate = currentVersion < betaVersion;
                

            
            // 只在首次检查时判断版本类型并保存
            if (await _localSettingsService.ReadSettingAsync<string>(KeyValues.LastUpdateCheckResult) == null)
            {
                // 如果之前已经判断过版本类型，直接使用已保存的设置
                string updateType = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateType) ?? string.Empty;
                string updateUrl = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateUrl) ?? string.Empty;
                
                // 如果没有保存过版本类型或URL，重新判断并保存
                if (string.IsNullOrEmpty(updateType) || string.IsNullOrEmpty(updateUrl))
                {
                    if (currentVersion < stableVersion)
                    {
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, "stable");
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, "https://apps.microsoft.com/detail/9p9cbkd5hr3w");
                    }
                    else
                    {
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, "beta");
                        await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, "https://t.me/potato_vn");
                    }
                }
            }

            // 保存更新检查结果
            await _localSettingsService.SaveSettingAsync(KeyValues.LastUpdateCheckResult, needsUpdate);

            return needsUpdate;
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