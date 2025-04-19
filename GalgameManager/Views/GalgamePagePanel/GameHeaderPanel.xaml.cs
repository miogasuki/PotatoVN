using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.GalgamePagePanel;

public partial class GameHeaderPanel
{
    private readonly ICategoryService _categoryService = App.GetService<ICategoryService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private readonly IFilterService _filterService = App.GetService<IFilterService>();
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();
    private readonly IStaffService _staffService = App.GetService<IStaffService>();
    private Galgame? _lastGame;
    private readonly ObservableCollection<GameHeaderPanelStaffList> _staffListSource = new();

    public GameHeaderPanel()
    {
        InitializeComponent();
        StaffList.ItemsSource = _staffListSource;
        Unloaded += (_, _) =>
        {
            _staffService.OnGameStaffChanged -= StaffServiceOnOnGameStaffChanged;
            _localSettingsService.OnSettingChanged -= LocalSettingsServiceOnOnSettingChanged;
            if (Game is not null)
                Game.HeaderImagePath.OnValueChanged -= HeaderImagePathOnOnValueChanged;
        };
        Loaded += (_, _) =>
        {
            _staffService.OnGameStaffChanged += StaffServiceOnOnGameStaffChanged;
            _localSettingsService.OnSettingChanged += LocalSettingsServiceOnOnSettingChanged;
        };
        return;

        void LocalSettingsServiceOnOnSettingChanged(string key, object? value)
        {
            switch (key)
            {
                case KeyValues.GalgamePageNewLayout_ShowPainter:
                case KeyValues.GalgamePageNewLayout_ShowSeiyu:
                case KeyValues.GalgamePageNewLayout_ShowWriter:
                case KeyValues.GalgamePageNewLayout_ShowMusician:
                    UiThreadInvokeHelper.Invoke(UpdateStaffs);
                    break;
                case KeyValues.GalgamePageNewLayout_CoverImage:
                case KeyValues.GalgamePageNewLayout_ShowHeaderImage:
                case KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground:
                    UiThreadInvokeHelper.Invoke(UpdateHeaderImgAndCoverImg);
                    break;
            }
        }
        
        void StaffServiceOnOnGameStaffChanged(Galgame obj)
        {
            UiThreadInvokeHelper.Invoke(async () =>
            {
                if (obj != Game) return;
                await UpdateStaffs(); //加载新的Staff数据
            });
        }
    }

    protected async override void Update()
    {
        try
        {
            if (_lastGame is not null) _lastGame.HeaderImagePath.OnValueChanged -= HeaderImagePathOnOnValueChanged;
            if (Game is null) return;
            _lastGame = Game;
            Game.HeaderImagePath.OnValueChanged += HeaderImagePathOnOnValueChanged;
            await UpdateHeaderImgAndCoverImg();
            await UpdateStaffs();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    private async void HeaderImagePathOnOnValueChanged(string? arg)
    {
        try
        {
            await UpdateHeaderImgAndCoverImg();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    private void ClickDeveloper(object sender, RoutedEventArgs e)
    {
        if (Game is null) return;
        Category? category = _categoryService.GetDeveloperCategory(Game);
        if (category is null)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: "HomePage_NoDeveloperCategory".GetLocalized());
            return;
        }
        _filterService.ClearFilters();
        _filterService.AddFilter(new CategoryFilter(category));
        _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }

    private void ClickStaff(object sender, RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton button || button.DataContext is not Staff staff) return;
        NavigationHelper.NavigateToStaffPage(_navigationService,
            new StaffViewModel.StaffPageNavigationParameter { Staff = staff });
    }

    private void TitleSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Game?.Name.Value?.Length * 40 ?? 0;
        TitleTextBlock.MaxWidth = Math.Max(Math.Min(e.NewSize.Width - 80, width), 50);
    }
    
    private async Task UpdateStaffs()
    {
        if (Game is null) return;
        List<(Career career, string settingKey)> careerSettings =
        [
            (Career.Painter, KeyValues.GalgamePageNewLayout_ShowPainter),
            (Career.Seiyu, KeyValues.GalgamePageNewLayout_ShowSeiyu),
            (Career.Writer, KeyValues.GalgamePageNewLayout_ShowWriter),
            (Career.Musician, KeyValues.GalgamePageNewLayout_ShowMusician)
        ];
        _staffListSource.Clear();
        foreach (var (career, settingKey) in careerSettings)
        {
            if (!await _localSettingsService.ReadSettingAsync<bool>(settingKey)) continue;
            
            List<Staff> tmp = _staffService.GetStaffs(Game).Where(s => (s.GetRelation(Game) ?? []).Contains(career))
                .ToList();
            if (tmp.Count == 0) continue;
            _staffListSource.Add(new GameHeaderPanelStaffList(career, tmp));
        }
        // 强制重新计算布局以解决重叠和顺序问题
        StaffList.InvalidateMeasure();
        StaffList.UpdateLayout();
    }

    private async Task UpdateHeaderImgAndCoverImg()
    {
        if (Game is null) return;
        // Header图是否应该显示
        var showBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage);
        if (showBackground && File.Exists(Game.HeaderImagePath.Value))
        {
            ContentRoot.Margin = new Thickness(0, 20, 0, 0); //有Header图要给整个控件加点边距让它看起来好看点
            Cover.Visibility = (!await _localSettingsService.ReadSettingAsync<bool>(KeyValues
                .GalgamePageNewLayout_ShowCoverWhenNoBackground)).ToVisibility();
        }
        else
            Cover.Visibility = Visibility.Visible;

        // 禁用了封面图显示
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_CoverImage) is false)
            Cover.Visibility = Visibility.Collapsed;
    }
}

public class GameHeaderPanelStaffList (Career career, List<Staff> staffsList)
{
    public string Career { get; set; } = $"Career_Relation_{career}".GetLocalized();
    public List<Staff> StaffsList { get; set; } = staffsList;
}