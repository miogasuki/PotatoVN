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
    }

    protected async override void Update()
    {
        try
        {
            if (Game is null)
            {
                if (_lastGame is not null) 
                    _lastGame.HeaderImagePath.OnValueChanged -= HeaderImagePathOnOnValueChanged;
                _staffListSource.Clear();
                return;
            }
            _lastGame = Game;
            Game.HeaderImagePath.OnValueChanged += HeaderImagePathOnOnValueChanged;

            if (File.Exists(Game.HeaderImagePath.Value))
            {
                // 如果存在背景图，则为内容添加上边距
                ContentRoot.Margin = new Thickness(0, 20, 0, 0);
                
                // 如果用户设置了"只在没有背景图时显示封面"，则隐藏封面
                if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground) is true)
                    Cover.Visibility = Visibility.Collapsed;
            }

            // 不论背景图是否存在，如果用户设置中禁用了封面图显示，则隐藏封面
            if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_CoverImage) is false)
                Cover.Visibility = Visibility.Collapsed;

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
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
        }
    }

    private void HeaderImagePathOnOnValueChanged(string? arg) => Update();

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
}

public class GameHeaderPanelStaffList (Career career, List<Staff> staffsList)
{
    public string Career { get; set; } = $"Career_Relation_{career}".GetLocalized();
    public List<Staff> StaffsList { get; set; } = staffsList;
}