using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views.Dialog;

public sealed partial class ManageGalgamePageLayoutDialog : ContentDialog
{
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();

    // 定义布局更改事件
    public static event EventHandler<bool>? LayoutChanged;

    public bool GalgamePageNewLayout { get; set; }
    public bool GalgamePageNewLayout_ShowPainter { get; set; }
    public bool GalgamePageNewLayout_ShowSeiyu { get; set; }
    public bool GalgamePageNewLayout_ShowWriter { get; set; }
    public bool GalgamePageNewLayout_ShowMusician { get; set; }
    public bool GalgamePageNewLayout_ShowBackground { get; set; }
    public bool GalgamePageNewLayout_ShowCover { get; set; }
    public bool GalgamePageNewLayout_ShowCoverWhenNoBackground { get; set; }
    public bool GalgamePageNewLayout_ShowExpectedPlayTime { get; set; }
    public bool GalgamePageNewLayout_ShowRating { get; set; }
    public bool GalgamePageNewLayout_ShowTags { get; set; }
    public bool GalgamePageNewLayout_ShowCharacters { get; set; }

    public ManageGalgamePageLayoutDialog()
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "ManageGalgamePageLayoutDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();

        LoadSettings();
        PrimaryButtonClick += ManageGalgamePageLayoutDialog_PrimaryButtonClick;
    }

    private async void LoadSettings()
    {
        GalgamePageNewLayout = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout);
        GalgamePageNewLayout_ShowPainter = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowPainter);
        GalgamePageNewLayout_ShowSeiyu = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowSeiyu);
        GalgamePageNewLayout_ShowWriter = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowWriter);
        GalgamePageNewLayout_ShowMusician = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowMusician);
        GalgamePageNewLayout_ShowBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage);
        GalgamePageNewLayout_ShowCover = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_CoverImage);
        GalgamePageNewLayout_ShowCoverWhenNoBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground);
        GalgamePageNewLayout_ShowExpectedPlayTime = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime);
        GalgamePageNewLayout_ShowRating = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowRating);
        GalgamePageNewLayout_ShowTags = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowTags);
        GalgamePageNewLayout_ShowCharacters = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCharacters);
    }

    private async void ManageGalgamePageLayoutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();

        // Save settings
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout, GalgamePageNewLayout);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowPainter, GalgamePageNewLayout_ShowPainter);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowSeiyu, GalgamePageNewLayout_ShowSeiyu);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowWriter, GalgamePageNewLayout_ShowWriter);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowMusician, GalgamePageNewLayout_ShowMusician);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowHeaderImage, GalgamePageNewLayout_ShowBackground);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_CoverImage, GalgamePageNewLayout_ShowCover);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground, GalgamePageNewLayout_ShowCoverWhenNoBackground);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime, GalgamePageNewLayout_ShowExpectedPlayTime);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowRating, GalgamePageNewLayout_ShowRating);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowTags, GalgamePageNewLayout_ShowTags);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowCharacters, GalgamePageNewLayout_ShowCharacters);

        // 触发事件通知
        LayoutChanged?.Invoke(this, GalgamePageNewLayout);

        deferral.Complete();
    }
}

