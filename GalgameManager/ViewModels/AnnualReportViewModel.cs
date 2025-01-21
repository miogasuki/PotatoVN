using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System.Text.RegularExpressions;

namespace GalgameManager.ViewModels;

public partial class AnnualReportViewModel(IGalgameCollectionService gameService, ICategoryService categoryService)
    : ObservableRecipient, INavigationAware
{
    [ObservableProperty] private Frame? _contentFrame;
    [ObservableProperty] private Visibility _calculating = Visibility.Visible;
    
    private int _currentPageIndex = -1;
    private int _previousPageIndex = -1;
    private readonly AnnualReportData _annualReportData = new();
    
    private static readonly Regex YearPattern = new(@"(19|20)\d{2}(-\d{1,2})?");
    
    public void OnNavigatedTo(object parameter)
    {
        // 计算年度报告数据
        Task.Run(async () =>
        {
            List<Galgame> gamesPlayThisYear = [];
            // 第一页数据
            foreach (Galgame game in gameService.Galgames)
            {
                var playInYearMin = 0;
                var playedInYear = new bool[12];
                foreach (KeyValuePair<string, int> t in game.PlayedTime)
                {
                    DateTime date = Utils.TryParseDateGuessCulture(t.Key);
                    if (date.Year != AnnualReportData.Year) continue;
                    playInYearMin += t.Value;
                    _annualReportData.PlayedTimePerMonth[date.Month - 1] += t.Value / 60.0; //确实会有误差，但应该问题不大
                    if (!playedInYear[date.Month - 1])
                    {
                        playedInYear[date.Month - 1] = true;
                        _annualReportData.PlayedGamesPerMonth[date.Month - 1]++;
                    }
                }
                if (playInYearMin >= _annualReportData.FavoriteGamePlayedTime * 60)
                {
                    _annualReportData.FavoriteGame = game;
                    _annualReportData.FavoriteGamePlayedTime = playInYearMin / 60.0;
                }
                if (playInYearMin > 0)
                {
                    _annualReportData.TotalGamesPlayed++;
                    gamesPlayThisYear.Add(game);
                    // 计算其属于哪个游戏时长区间
                    var found = false;
                    for (var i = 1; i < AnnualReportData.PlayedTimeRange.Length; i++)
                    {
                        if (playInYearMin <= AnnualReportData.PlayedTimeRange[i] * 60)
                        {
                            _annualReportData.PlayedTimeRangeCnt[i-1]++;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        _annualReportData.PlayedTimeRangeCnt[^1]++;
                    // 游玩状态统计
                    if (!_annualReportData.PlayTypeCnt.TryAdd(game.PlayType, 1))
                        _annualReportData.PlayTypeCnt[game.PlayType]++;
                }
                _annualReportData.PlayedTime += playInYearMin / 60.0;
            }
            // 第二页数据
            Dictionary<string, int> tags = new();
            foreach (Galgame game in gameService.Galgames)
            {
                foreach (var tag in game.Tags.Value ?? [])
                {
                    if (!tags.TryAdd(tag, 1))
                        tags[tag]++;
                }
            }
            // 可以优化成nlogn的，但应该不会造成太大的性能问题（应该不会有几十万个tag吧？）
            _annualReportData.TagFrequencies = tags
                .OrderByDescending(p => p.Value)
                .Where(g => !AnnualReportData.BannedTags.Contains(g.Key) && 
                            !YearPattern.IsMatch(g.Key))
                .Take(AnnualReportData.TagFrequencyMax)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            // 第三页数据
            // 以下部分效率很低，但是数据量不大，应该问题不大
            Dictionary<Category, int> developers = new();
            CategoryGroup devGroup = categoryService.DeveloperGroup;
            foreach (Galgame game in gamesPlayThisYear)
            {
                foreach (Category category in game.Categories.Where(c => devGroup.Categories.Contains(c)))
                {
                    if (!developers.TryAdd(category, 1))
                        developers[category]++;
                }
            }
            _annualReportData.FavouriteDeveloper = developers
                .OrderByDescending(p => p.Value)
                .FirstOrDefault().Key ?? new Category();
            _annualReportData.GamesInFavouriteDeveloper.AddRange(gamesPlayThisYear.Where(game =>
                game.Categories.Contains(_annualReportData.FavouriteDeveloper)));
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                Calculating = Visibility.Collapsed;
                NavigateToPage(0);
            });
        });
    }

    public void OnNavigatedFrom()
    {
    }

    public void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ContentFrame == null) return;
        NavigateToPage(sender.Items.IndexOf(sender.SelectedItem));
    }

    [RelayCommand]
    private void NextPage()
    {
        if (ContentFrame == null) return;
        NavigateToPage(Math.Min(_currentPageIndex + 1, 2));
        UpdateSelectorBarSelection();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (ContentFrame == null) return;
        NavigateToPage(Math.Max(_currentPageIndex - 1, 0));
        UpdateSelectorBarSelection();
    }

    private void UpdateSelectorBarSelection()
    {
        var selectorBar = ContentFrame?.Parent is Grid grid 
            ? grid.Children.OfType<SelectorBar>().FirstOrDefault() 
            : null;

        if (selectorBar != null && _currentPageIndex >= 0 && _currentPageIndex < selectorBar.Items.Count)
        {
            selectorBar.SelectedItem = selectorBar.Items[_currentPageIndex];
        }
    }

    private void NavigateToPage(int pageIndex)
    {
        if (ContentFrame == null || _currentPageIndex == pageIndex) return;

        Type pageType = pageIndex switch
        {
            0 => typeof(Views.AnnualReportSubPage1),
            1 => typeof(Views.AnnualReportSubPage2),
            2 => typeof(Views.AnnualReportSubPage3),
            _ => typeof(Views.AnnualReportSubPage1)
        };

        _previousPageIndex = _currentPageIndex;

        SlideNavigationTransitionEffect slideNavigationTransitionEffect = pageIndex - _previousPageIndex > 0 
            ? SlideNavigationTransitionEffect.FromRight 
            : SlideNavigationTransitionEffect.FromLeft;

        _currentPageIndex = pageIndex;
        ContentFrame.Navigate(pageType, _annualReportData, new SlideNavigationTransitionInfo() 
        { 
            Effect = slideNavigationTransitionEffect 
        });
    }
}


/// <summary>
/// 存放年度报告数据
/// </summary>
public partial class AnnualReportData : ObservableObject
{
    public const int Year = 2024;
    public const int TagFrequencyMax = 30;
    public static readonly int[] PlayedTimeRange = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60];
    public static readonly string[] BannedTags = ["PC", "汉化", "GAL", "galgame", "Galgame", "ADV", "R18", "AVG","游戏","生肉","硬盘已存"];
    
    [ObservableProperty] private Galgame _favoriteGame = new();
    [ObservableProperty] private double _favoriteGamePlayedTime; //最喜欢的游戏的时间，小时
    [ObservableProperty] private double _playedTime; //玩过的总时间，小时
    /// 每个月的玩过的时间，小时
    public double[] PlayedTimePerMonth = new double[12]; 
    /// 每个月玩过的游戏数
    public int[] PlayedGamesPerMonth = new int[12]; 
    /// 玩过的游戏数
    public int TotalGamesPlayed;
    /// 每个游戏时长区间的游戏数，只统计本年度玩过的游戏。下标表示从PlayedTimeRange[i]到PlayedTimeRange[i+1]的区间，
    /// 若i为最后一个元素，则表示PlayedTimeRange[i]+的区间
    public int[] PlayedTimeRangeCnt = new int[PlayedTimeRange.Length];
    public Dictionary<PlayType, int> PlayTypeCnt = new(); //玩过的游戏状态统计
    public Dictionary<string, int> TagFrequencies = new(); //Tag词频统计
    [ObservableProperty] private Category _favouriteDeveloper = new(); //最喜欢的开发商
    public List<Galgame> GamesInFavouriteDeveloper = new(); //今年玩过的最喜欢的开发商的游戏
}
