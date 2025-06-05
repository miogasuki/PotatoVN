using Windows.Storage;
using Windows.ApplicationModel;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Net.Http;
using System.IO;

namespace GalgameManager.Services;

public class UpdateService : IUpdateService
{
    private readonly bool _firstUpdate;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInfoService _infoService;
    
    // 本次启动是否已经更新失败过的标记
    private bool _updateFailedThisSession;
    // 本次启动是否已经取消过更新的标记
    private bool _updateCancelledThisSession;

    public event Action<bool>? SettingBadgeEvent;

    public UpdateService(ILocalSettingsService localSettingsService, IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _infoService = infoService;
        var last = localSettingsService.ReadSettingAsync<string>(KeyValues.DisplayedUpdateVersion).Result ?? "";
        _firstUpdate = last != RuntimeHelper.GetVersion();
    }

    public async Task<bool> CheckUpdateAsync()
    {
        try
        {
            HttpClient client = Utils.GetDefaultHttpClient();
            HttpResponseMessage response = await client.GetAsync(
                "https://potatovn.net/raw/version.html");
            var versionString = (await response.Content.ReadAsStringAsync())
                            .Replace("\n", "").Replace("\r","");
            
            // 分割版本号，获取正式版和测试版版本
            var versions = versionString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            // if (versions.Length < 2)
            // {
            //     // 格式不符合预期，无法判断
            //     return false;
            // }
            
            
            Version stableVersion = Version.Parse(versions[0].Trim());
            Version betaVersion = Version.Parse(versions[1].Trim());
            Version currentVersion = Version.Parse(RuntimeHelper.GetVersion());
            
            // 判断当前版本是否需要更新
            bool needsUpdate;
                
            // 检测包名以确定版本类型
            var isStoreVersion = IsStoreVersion();
            
            // 根据版本类型和当前版本确定更新信息
            if (isStoreVersion)
            {
                // 商店版：检查是否有新的稳定版
                needsUpdate = currentVersion < stableVersion;
                await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, "stable");
                await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, "https://apps.microsoft.com/detail/9p9cbkd5hr3w");
            }
            else
            {
                // 侧载版：检查是否有新的测试版
                needsUpdate = currentVersion < betaVersion;
                await _localSettingsService.SaveSettingAsync(KeyValues.UpdateType, "beta");
                await _localSettingsService.SaveSettingAsync(KeyValues.UpdateUrl, "http://localhost:5000/download");
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

    /// <summary>
    /// 检测当前是否为商店版
    /// </summary>
    /// <returns>true表示商店版，false表示侧载版</returns>
    private static bool IsStoreVersion()
    {
        try
        {
            if (!RuntimeHelper.IsMSIX) return false;
            
            var packageName = Package.Current.Id.Name;
            // 商店版的包名是固定的，其他都是侧载版
            return packageName == "37126GoldenPotato137.PotatoVN";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查是否有新版本且未被忽略
    /// </summary>
    /// <returns>有可用更新返回版本号，否则返回null</returns>
    public async Task<string?> GetAvailableUpdateVersionAsync()
    {
        try
        {
            // 如果本次启动已经更新失败过或用户已取消过，不再返回可用更新
            if (_updateFailedThisSession || _updateCancelledThisSession) return null;
            
            // 检查是否有更新
            var hasUpdate = await CheckUpdateAsync();
            if (!hasUpdate) return null;

            // 获取远程版本号
            var updateType = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateType) ?? "stable";
            var isStoreVersion = updateType == "stable";
            
            // 获取忽略的版本列表
            List<string> ignoredVersions = await _localSettingsService.ReadSettingAsync<List<string>>(KeyValues.IgnoredUpdateVersions) ?? new List<string>();
            
            // For test only (从之前用户的修改中获取)
            var versions = new[] { "1.9.5.0", "1.9.5.1" };
            
            var targetVersion = isStoreVersion ? versions[0] : versions[1];
            
            // 检查目标版本是否被忽略
            if (ignoredVersions.Contains(targetVersion))
            {
                return null; // 该版本已被忽略
            }
            
            return targetVersion;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 显示更新确认对话框
    /// </summary>
    /// <returns>用户选择：0=取消，1=立即更新，2=忽略这个版本</returns>
    public async Task<int> ShowUpdateConfirmationAsync()
    {
        try
        {
            var updateType = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateType) ?? "stable";
            var isStoreVersion = updateType == "stable";
            
            var title = "UpdateService_UpdateAvailable_Title".GetLocalized();
            var content = isStoreVersion 
                ? "UpdateService_StoreUpdate_Content".GetLocalized()
                : "UpdateService_SideloadUpdate_Content".GetLocalized();
            
            ContentDialog updateDialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
                RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
                Title = title,
                Content = content,
                PrimaryButtonText = "UpdateService_Update_Confirm".GetLocalized(),
                SecondaryButtonText = "UpdateService_Update_Ignore".GetLocalized(),
                CloseButtonText = "UpdateService_Update_Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await updateDialog.ShowAsync();
            return result switch
            {
                ContentDialogResult.Primary => 1,    // 立即更新
                ContentDialogResult.Secondary => 2,  // 忽略这个版本
                _ => 0                               // 取消/稍后提醒
            };
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Dialog_Error".GetLocalized(), ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// 忽略指定版本的更新
    /// </summary>
    /// <param name="version">要忽略的版本号</param>
    public async Task IgnoreVersionAsync(string version)
    {
        try
        {
            List<string> ignoredVersions = await _localSettingsService.ReadSettingAsync<List<string>>(KeyValues.IgnoredUpdateVersions) ?? new List<string>();
            if (!ignoredVersions.Contains(version))
            {
                ignoredVersions.Add(version);
                await _localSettingsService.SaveSettingAsync(KeyValues.IgnoredUpdateVersions, ignoredVersions);
            }
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_IgnoreVersion_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 执行更新操作
    /// </summary>
    public async Task PerformUpdateAsync()
    {
        try
        {
            var updateType = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateType) ?? "stable";
            var isStoreVersion = updateType == "stable";
            
            if (isStoreVersion)
            {
                // 商店版：打开商店链接
                await OpenStoreForUpdateAsync();
            }
            else
            {
                // 侧载版：下载更新包并准备在应用退出后安装
                await DownloadAndPrepareUpdateAsync();
            }
        }
        catch (Exception ex)
        {
            // 设置本次启动更新失败标记
            _updateFailedThisSession = true;
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Update_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 打开商店进行更新
    /// </summary>
    private async Task OpenStoreForUpdateAsync()
    {
        try
        {
            var storeUrl = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateUrl) ?? 
                           "https://apps.microsoft.com/detail/9p9cbkd5hr3w";
            await Launcher.LaunchUriAsync(new Uri(storeUrl));
            _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Store_Opened".GetLocalized());
        }
        catch (Exception ex)
        {
            // 设置本次启动更新失败标记
            _updateFailedThisSession = true;
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Store_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 下载更新包并准备在应用退出后安装
    /// </summary>
    private async Task DownloadAndPrepareUpdateAsync()
    {
        try
        {
            _infoService.Info(InfoBarSeverity.Informational, "UpdateService_Download_Started".GetLocalized());
            
            var downloadUrl = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateUrl) ?? 
                              "http://localhost:5000/download";
            
            // 创建临时下载目录
            StorageFolder? tempFolder = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
                "UpdateDownload", CreationCollisionOption.ReplaceExisting);
            
            // 下载更新包
            HttpClient client = Utils.GetDefaultHttpClient();
            HttpResponseMessage response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            
            StorageFile? msixFile = await tempFolder.CreateFileAsync("PotatoVN_Update.msix", CreationCollisionOption.ReplaceExisting);
            await using (Stream? fileStream = await msixFile.OpenStreamForWriteAsync())
            {
                await response.Content.CopyToAsync(fileStream);
            }
            
            _infoService.Info(InfoBarSeverity.Success, "UpdateService_Download_Completed".GetLocalized());
            
            // 创建安装批处理文件
            await CreateInstallBatchFileAsync(msixFile.Path);
            
            // 询问是否立即重启安装
            await ShowInstallConfirmationAsync();
        }
        catch (Exception ex)
        {
            // 设置本次启动更新失败标记
            _updateFailedThisSession = true;
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Download_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 创建安装批处理文件，用于在应用退出后安装更新
    /// </summary>
    /// <param name="msixPath">MSIX文件路径</param>
    private async Task CreateInstallBatchFileAsync(string msixPath)
    {
        try
        {
            StorageFolder? tempFolder = ApplicationData.Current.TemporaryFolder;
            StorageFile? batchFile = await tempFolder.CreateFileAsync("InstallUpdate.bat", CreationCollisionOption.ReplaceExisting);
            
            // 获取当前应用的包信息用于重启
            var packageName = Package.Current.Id.FamilyName;
            var appId = Package.Current.Id.Name;
            
            // 批处理脚本内容：等待应用关闭，然后安装更新包，安装成功后重启应用，最后清理临时文件
            var batchContent = $@"@echo off
REM 等待应用完全关闭
timeout /t 3 /nobreak > nul

REM 安装更新包
powershell.exe -Command ""Add-AppxPackage -Path '{msixPath}'"" > nul 2>&1

REM 检查安装结果
if %errorlevel% == 0 (
    echo Update installed successfully
    REM 等待一下确保安装完成
    timeout /t 2 /nobreak > nul
    REM 重新启动应用
    start """" explorer.exe shell:appsFolder\{packageName}!{appId}
) else (
    echo Update installation failed
)

REM 清理临时文件
del ""{msixPath}"" > nul 2>&1
del ""%~f0"" > nul 2>&1
";
            
            await FileIO.WriteTextAsync(batchFile, batchContent);
            
            // 保存批处理文件路径
            await _localSettingsService.SaveSettingAsync(KeyValues.UpdateBatchPath, batchFile.Path);
        }
        catch (Exception ex)
        {
            throw new Exception($"创建安装脚本失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示安装确认对话框
    /// </summary>
    private async Task ShowInstallConfirmationAsync()
    {
        try
        {
            ContentDialog installDialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
                RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
                Title = "UpdateService_Install_Title".GetLocalized(),
                Content = "UpdateService_Install_Content".GetLocalized(),
                PrimaryButtonText = "UpdateService_Install_Confirm".GetLocalized(),
                CloseButtonText = "UpdateService_Install_Later".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await installDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // 执行安装并退出应用
                await ExecuteInstallAndExitAsync();
            }
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Install_Dialog_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 执行安装批处理并退出应用
    /// </summary>
    private async Task ExecuteInstallAndExitAsync()
    {
        try
        {
            var batchPath = await _localSettingsService.ReadSettingAsync<string>(KeyValues.UpdateBatchPath) ?? "";
            if (string.IsNullOrEmpty(batchPath) || !File.Exists(batchPath))
            {
                throw new Exception("安装脚本文件不存在");
            }
            
            // 启动批处理文件（异步执行，不等待结果）
            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            
            // 短暂延迟确保批处理启动
            await Task.Delay(500);
            
            // 退出应用
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Error, "UpdateService_Install_Execute_Error".GetLocalized(), ex.Message);
        }
    }

    /// <summary>
    /// 设置用户取消更新标记
    /// </summary>
    public void SetUpdateCancelledThisSession()
    {
        _updateCancelledThisSession = true;
    }

    public async Task UpdateSettingsBadgeAsync()
    {
        // 每次都检查是否有可用更新（未被忽略的且本次启动未失败或取消的）
        var availableVersion = await GetAvailableUpdateVersionAsync();
        SettingBadgeEvent?.Invoke(availableVersion != null);
    }

    public bool ShouldDisplayUpdateContent() => _firstUpdate;
}