using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using GalgameManager.Enums;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using WinRT;
using Windows.System;

namespace GalgameManager.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string SettingsKey = "AppBackgroundRequestedTheme";
    private const string BackdropSettingsKey = "AppBackgroundMaterial";

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    private readonly ILocalSettingsService _localSettingsService;

    public ThemeSelectorService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        Theme = await LoadThemeFromSettingsAsync();
        await Task.CompletedTask;
    }

    public async Task SetThemeAsync(ElementTheme theme)
    {
        Theme = theme;

        await SetRequestedThemeAsync();
        await SaveThemeInSettingsAsync(Theme);
    }

    public async Task SetRequestedThemeAsync()
    {
        if (App.MainWindow!.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = Theme;
            TitleBarHelper.UpdateTitleBar(Theme);
        }

        await Task.CompletedTask;
    }

    private async Task<ElementTheme> LoadThemeFromSettingsAsync()
    {
        var themeName = await _localSettingsService.ReadSettingAsync<string>(SettingsKey);

        if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
        {
            return cacheTheme;
        }

        return ElementTheme.Default;
    }

    private async Task SaveThemeInSettingsAsync(ElementTheme theme)
    {
        await _localSettingsService.SaveSettingAsync(SettingsKey, theme.ToString());
    }

    public async Task SetBackgroundMaterialAsync()
    {
        if (App.MainWindow?.Content is not FrameworkElement )
        {
            return;
        }

        // 获取当前背景材质类型
        var material = await _localSettingsService.ReadSettingAsync<BackgroundMaterialEnum>(KeyValues.BackgroundMaterial);

        // 获取主窗口
        var appWindow = App.MainWindow;
        if (appWindow == null) return;
        // 根据材质类型设置背景
        switch (material)
        {
            case BackgroundMaterialEnum.Mica:
                appWindow.SystemBackdrop = new MicaBackdrop();
                break;
            case BackgroundMaterialEnum.MicaAlt:
                appWindow.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
                break;
            case BackgroundMaterialEnum.DesktopAcrylic:
                appWindow.SystemBackdrop = new DesktopAcrylicBackdrop();
                break;
        }

        await Task.CompletedTask;
    }
    
}
