using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class LibraryViewModel(
    INavigationService navigationService,
    IGalgameSourceCollectionService galSourceService,
    IInfoService infoService,
    IBgTaskService bgTaskService,
    IGalgameCollectionService galgameService, // 注入 IGalgameCollectionService
    ILocalSettingsService settingsService
    )
    : ObservableObject, INavigationAware
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsBackEnabled))]
    private GalgameSourceBase? _currentSource;
    private GalgameSourceBase? _lastBackSource;
    private static GalgameSourceBase? _beforeNavigateFromSource; //用于从该页跳转到Galgame详情界面后返回时直接回到某个库的界面
    private static readonly List<GetGalgameInfoFromRssTask> RssTasks = [];

    [ObservableProperty]
    private AdvancedCollectionView _source = null!;
    public AdvancedCollectionView Galgames = new(new ObservableCollection<Galgame>());

    #region UI

    public readonly string UiSearch = "Search".GetLocalized();
    public bool IsBackEnabled => CurrentSource != null;
    [ObservableProperty] private bool _sourceVisible;
    [ObservableProperty] private bool _galgamesVisible;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty]
    private ObservableCollection<GalgameSourceBase> _pathNodes = new();
    [ObservableProperty] private bool _isNavBarVisible;
    [ObservableProperty] private bool _isStatisticsVisible;
    [ObservableProperty] private bool _displayPlayTypePolygon = true; // 是否显示游玩状态的小三角形

    partial void OnIsNavBarVisibleChanged(bool value)
    {
        if (settingsService.ReadSettingAsync<bool>(KeyValues.LibraryNavBar).Result != value)
            settingsService.SaveSettingAsync(KeyValues.LibraryNavBar, value);
    }

    partial void OnIsStatisticsVisibleChanged(bool value)
    {
        if (settingsService.ReadSettingAsync<bool>(KeyValues.LibraryStatistics).Result != value)
            settingsService.SaveSettingAsync(KeyValues.LibraryStatistics, value);
    }

    partial void OnDisplayPlayTypePolygonChanged(bool value)
    {
        if (settingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon).Result != value)
            settingsService.SaveSettingAsync(KeyValues.DisplayPlayTypePolygon, value);
    }

    #endregion

    #region SERACH

    [ObservableProperty] private string _searchTitle = "Search".GetLocalized();
    [ObservableProperty] private string _searchKey = "";
    [ObservableProperty] private ObservableCollection<string> _searchSuggestions = new();
    [ObservableProperty] private bool _updateGridSpacing;

    [RelayCommand]
    private void Search(string searchKey)
    {
        SearchTitle = searchKey == string.Empty ? UiSearch : UiSearch + " ●";
        Source.RefreshFilter();
    }

    #endregion

    [ObservableProperty]
    private string _statisticsText = string.Empty;

    private void UpdateStatistics()
    {
        var sourceCount = Source.Count;
        var galgameCount = Galgames.Count;
        StatisticsText = string.Format("LibraryPage_Statistics".GetLocalized(), sourceCount, galgameCount);
    }

    public void OnNavigatedTo(object parameter)
    {
        // 加载排序设置
        CurrentSortKey = (SortKeys)settingsService.ReadSettingAsync<int>(KeyValues.LibrarySortKey).Result;
        GameSortDescending = settingsService.ReadSettingAsync<bool>(KeyValues.LibraryGameSortDescending).Result;

        CurrentFolderSortKey = (GalgameSourceSortKeys)settingsService.ReadSettingAsync<int>(KeyValues.LibraryFolderSortKey).Result;
        FolderSortDescending = settingsService.ReadSettingAsync<bool>(KeyValues.LibraryFolderSortDescending).Result;

        Source = new AdvancedCollectionView(new ObservableCollection<IDisplayableGameObject>(), true)
        {
            Filter = s =>
            {
                if (s is GalgameSourceBase source)
                    return SearchKey.IsNullOrEmpty() || source.ApplySearchKey(SearchKey);
                if (s is Galgame game)
                    return SearchKey.IsNullOrEmpty() || game.ApplySearchKey(SearchKey);
                return false;
            }
        };
        IsStatisticsVisible = settingsService.ReadSettingAsync<bool>(KeyValues.LibraryStatistics).Result;
        IsNavBarVisible = settingsService.ReadSettingAsync<bool>(KeyValues.LibraryNavBar).Result;
        DisplayPlayTypePolygon = settingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon).Result;
        if (_beforeNavigateFromSource is not null) parameter = _beforeNavigateFromSource;
        NavigateTo(parameter as GalgameSourceBase); //显示根库 / 指定库
        _beforeNavigateFromSource = null;
        galSourceService.OnSourceChanged += HandleSourceCollectionChanged;
        foreach (GetGalgameInfoFromRssTask task in RssTasks.Where(t => t.IsRunning))
            task.OnProgress += HandleGetGalInfoProgressChanged;
    }

    public void OnNavigatedFrom()
    {
        galSourceService.OnSourceChanged -= HandleSourceCollectionChanged;
        _lastBackSource = CurrentSource = null;
        foreach (GetGalgameInfoFromRssTask task in RssTasks)
            task.OnProgress -= HandleGetGalInfoProgressChanged;
        List<GetGalgameInfoFromRssTask> toRemove = RssTasks.Where(t => !t.IsRunning).ToList();
        foreach (GetGalgameInfoFromRssTask task in toRemove) RssTasks.Remove(task);
    }

    private void HandleSourceCollectionChanged()
    {
        CurrentSource = _lastBackSource = null;
        NavigateTo(null);
    }

    /// <summary>
    /// 点击了某个库（若clickItem为null则显示所有根库）<br/>
    /// 若这个库有子库，保持在LibraryViewModel界面，否则以库为Filter进入主界面
    /// </summary>
    [RelayCommand]
    private void NavigateTo(IDisplayableGameObject? clickedItem)
    {
        UpdateGridSpacing = false;
        Source.Clear();
        Galgames.Clear();
        if (clickedItem == null)
        {
            foreach (GalgameSourceBase src in galSourceService.GetGalgameSources()
                         .Where(s => s.ParentSource is null))
                Source.Add(src);
        }

        if (clickedItem is Galgame galgame)
        {
            _beforeNavigateFromSource = CurrentSource;
            navigationService.NavigateTo(typeof(GalgameViewModel).FullName!,
                new GalgamePageParameter { Galgame = galgame });
        }
        else if (clickedItem is GalgameSourceBase source)
        {
            if (source.SubSources.Count > 0)
            {
                foreach (GalgameSourceBase src in galSourceService.GetGalgameSources()
                             .Where(s => s.ParentSource == clickedItem))
                    Source.Add(src);
                foreach (GalgameAndPath game in source.Galgames)
                    Galgames.Add(game.Galgame);
            }
            else
            {
                // _filterService.ClearFilters();
                // _filterService.AddFilter(new SourceFilter(source));
                // _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
                foreach (GalgameAndPath game in source.Galgames)
                    Galgames.Add(game.Galgame);
            }
            source.LastClicked = DateTime.Now;
            galSourceService.Save(source);
            CurrentSource = source;
        }
        else if (clickedItem is null)
            CurrentSource = null;
        UpdateGridSpacing = true;
        SourceVisible = Source.Count > 0;
        GalgamesVisible = Galgames.Count > 0;
        UpdateStatistics();

        // 应用排序
        ApplySorting();

        // 更新路径节点
        PathNodes.Clear();
        PathNodes.Add(new GalgameFolderSource{Name = "LibraryPage_Root".GetLocalized()});
        if (clickedItem is GalgameSourceBase newSource)
        {
            GalgameSourceBase? currentSource = newSource;
            List<GalgameSourceBase> nodes = new List<GalgameSourceBase>();
            while (currentSource != null)
            {
                nodes.Insert(0, currentSource);
                currentSource = currentSource.ParentSource;
            }
            foreach (GalgameSourceBase node in nodes)
            {
                PathNodes.Add(node);
            }
        }
    }
    

    [RelayCommand]
    public void Back()
    {
        if (CurrentSource is null) return;
        _lastBackSource = CurrentSource;
        NavigateTo(CurrentSource.ParentSource);
    }

    [RelayCommand]
    private void Forward()
    {
        if (_lastBackSource is null || _lastBackSource == CurrentSource) return;
        NavigateTo(_lastBackSource);
    }

    [RelayCommand]
    private async Task GetInfoFromRss()
    {
        CheckBox includeSubfoldersCheckBox = new()
        {
            Content = "LibraryPage_GetInfoFromRss_IncludeSubfolders".GetLocalized(),
            IsChecked = true
        };

        StackPanel dialogContent = new ()
        {
            Spacing = 10
        };
        dialogContent.Children.Add(new TextBlock { Text = "LibraryPage_GetInfoFromRss_Content".GetLocalized() });
        dialogContent.Children.Add(includeSubfoldersCheckBox);
        
        ContentDialog dialog = new ()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "LibraryPage_GetInfoFromRss_Title".GetLocalized(),
            Content = dialogContent,
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var scanSubfolders = includeSubfoldersCheckBox.IsChecked ?? true;
        
        List<GalgameSourceBase> sources = new();
        if (CurrentSource is null)
        {
            sources.AddRange(galSourceService.GetGalgameSources());
        }
        else
        {
            sources.Add(CurrentSource);
            if (scanSubfolders)
            {
                ObservableCollection<GalgameSourceBase> allSources = galSourceService.GetGalgameSources();
                AddSubSources(CurrentSource, allSources);
            }
        }
        
        foreach (GalgameSourceBase source in sources)
        {
            GetGalgameInfoFromRssTask getGalgameInfoFromRss = new GetGalgameInfoFromRssTask(source);
            getGalgameInfoFromRss.OnProgress += HandleGetGalInfoProgressChanged;
            RssTasks.Add(getGalgameInfoFromRss);
            _ = bgTaskService.AddBgTask(getGalgameInfoFromRss);
        }
        
        return;
        
        void AddSubSources(GalgameSourceBase parent, IEnumerable<GalgameSourceBase> allSources)
        {
            foreach (GalgameSourceBase source in allSources.Where(s => s.ParentSource == parent))
            {
                sources.Add(source);
                AddSubSources(source, allSources);
            }
        }
    }

    [RelayCommand]
    private async Task AddLibrary()
    {
        try
        {
            AddSourceDialog dialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
            };
            await dialog.ShowAsync();
            if (dialog.Canceled) return;
            switch (dialog.SelectItem)
            {
                case 0:
                    await galSourceService.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, dialog.Path);
                    break;
                case 1:
                    await galSourceService.AddGalgameSourceAsync(GalgameSourceType.LocalZip, dialog.Path);
                    break;
            }

        }
        catch (Exception e)
        {
            infoService.Info(InfoBarSeverity.Error, msg: e.Message);
        }
    }

    [RelayCommand]
    private void EditLibrary(GalgameSourceBase? source)
    {
        if (source is null) return;
        if (source is VirtualSource virtualSource) virtualSource.UpdateGames(galgameService.Galgames);
        _beforeNavigateFromSource = CurrentSource;
        navigationService.NavigateTo(typeof(GalgameSourceViewModel).FullName!, source.Url);
    }

    [RelayCommand]
    private async Task DeleteFolder(GalgameSourceBase? galgameFolder)
    {
        if (galgameFolder is null) return;
        if (!galgameFolder.IsDelectable)
        {
            infoService.Info(InfoBarSeverity.Error, msg: "LibraryPage_CannotDelete".GetLocalized());
            return;
        }
        await galSourceService.DeleteGalgameFolderAsync(galgameFolder);
    }

    [RelayCommand]
    private async Task ScanAll()
    {
        CheckBox includeSubfoldersCheckBox = new ()
        {
            Content = "LibraryPage_ScanAll_IncludeSubfolders".GetLocalized(),
            IsChecked = true
        };

        StackPanel dialogContent = new ()
        {
            Spacing = 10
        };
        dialogContent.Children.Add(new TextBlock { Text = "LibraryPage_ScanAll_Content".GetLocalized() });
        dialogContent.Children.Add(includeSubfoldersCheckBox);

        ContentDialog dialog = new ()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "LibraryPage_ScanAll_Title".GetLocalized(),
            Content = dialogContent,
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var scanSubfolders = includeSubfoldersCheckBox.IsChecked ?? true;
        
        if (CurrentSource is null)
        {
            galSourceService.ScanAll();
            infoService.Info(InfoBarSeverity.Success, msg: "LibraryPage_ScanAll_Success".GetLocalized(Source.Count));
        }
        else
        {
            List<GalgameSourceBase> sources = [CurrentSource]; 

            // 如果用户选择包含子文件夹，则添加所有子库
            if (scanSubfolders)
            {
                ObservableCollection<GalgameSourceBase> allSources = galSourceService.GetGalgameSources();
                AddSubSources(CurrentSource, allSources);
            }
            
            foreach (GalgameSourceBase source in sources)
            {
                galSourceService.Scan(source);
                infoService.Info(InfoBarSeverity.Success, msg: "LibraryPage_Scan_Success".GetLocalized(source.Name));
            }
            
            return;
            
            void AddSubSources(GalgameSourceBase parent, IEnumerable<GalgameSourceBase> allSources)
            {
                foreach (GalgameSourceBase source in allSources.Where(s => s.ParentSource == parent))
                {
                    sources.Add(source);
                    AddSubSources(source, allSources);
                }
            }
        }
    }

    [RelayCommand]
    private void EditCurrentFolder()
    {
        if (CurrentSource is null) return;
        _beforeNavigateFromSource = CurrentSource;
        navigationService.NavigateTo(typeof(GalgameSourceViewModel).FullName!, CurrentSource.Url);
    }

    #region MenuFlyout

    [ObservableProperty] private Galgame? _currentContextGame; // 当前右键菜单上下文游戏对象

    [RelayCommand]
    private async Task GalFlyOutDelete(Galgame? galgame)
    {
        if(galgame == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "HomePage_Remove_Title".GetLocalized(),
            Content = "HomePage_Remove_Message".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await galgameService.RemoveGalgame(galgame);
        };
        
        await dialog.ShowAsync();

    }

    [RelayCommand]
    private void GalFlyOutEdit(Galgame? galgame)
    {
        if(galgame == null) return;
        _beforeNavigateFromSource = CurrentSource;
        navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame);
    }

    [RelayCommand]
    private async Task GalFlyOutGetInfoFromRss(Galgame? galgame)
    {
        if(galgame == null) return;
        IsPhrasing = true;
        await galgameService.PhraseGalInfoAsync(galgame);
        IsPhrasing = false;
    }

    [RelayCommand]
    private void SetCurrentContextGame(Galgame? game)
    {
        CurrentContextGame = game;
    }

    [RelayCommand]
    private async Task GalFlyOutChangePlayStatus(string playTypeString)
    {
        if (CurrentContextGame == null) return;
        
        if (!Enum.TryParse(playTypeString, out PlayType playType))
            return;

        CurrentContextGame.PlayType = playType;
        
        await galgameService.SaveGalgameAsync(CurrentContextGame); 
        
    }

    public bool IsCurrentPlayType(Galgame? game, string playTypeString)
    {
        if (game == null) return false;

        if (!Enum.TryParse(playTypeString, out PlayType playType))
            return false;
            
        return game.PlayType == playType;
    }

    [RelayCommand]
    private async Task ShowChangePlayStatusDialog(Galgame? game)
    {
        if (game == null) return;
        
        ChangePlayStatusDialog dialog = new ChangePlayStatusDialog(game);
        await dialog.ShowAsync();
        
        if (!dialog.Canceled)
        {
            await galgameService.SaveGalgameAsync(game); 
            
        }
    }

    public void GalFlyout_Opening(object sender, object e)
    {
        if (sender is MenuFlyout flyout && flyout.Target != null)
        {
            Galgame? game = flyout.Target.DataContext as Galgame;
            SetCurrentContextGame(game);
        }
    }

    #endregion

    public void OnBreadcrumbBarItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is GalgameFolderSource folder && folder.Name == "LibraryPage_Root".GetLocalized())
            NavigateTo(null);
        else if (args.Item is GalgameSourceBase source)
        {
            NavigateTo(source);
        }
    }

    partial void OnCurrentSourceChanged(GalgameSourceBase? oldValue, GalgameSourceBase? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.GalgamesChanged -= HandleGalgamesChanged;
        }
        
        if (newValue is not null)
        {
            newValue.GalgamesChanged += HandleGalgamesChanged;
            if (newValue is VirtualSource virtualSource) virtualSource.UpdateGames(galgameService.Galgames); 
        }
    }

    private void HandleGalgamesChanged(Galgame galgame, bool isRemoved)
    {
        // 只刷新游戏列表，不刷新整个页面
        if (isRemoved)
        {
            Galgames.Remove(galgame);
        }
        else if (!Galgames.Contains(galgame))
        {
            Galgames.Add(galgame);
        }
        UpdateStatistics();

        // 重新应用排序，启用后会导致刷新整个页面，覆盖原有的动画效果
        // ApplySorting();
    }
    
    private void HandleGetGalInfoProgressChanged(Progress progress)
    {
        infoService.Info(progress.ToSeverity(), msg: progress.Message, displayTimeMs: progress.ToSeverity() switch
        {
            InfoBarSeverity.Informational => 300000,
            _ => 3000
        });
    }

    #region SORTING
    // 为XAML绑定添加静态枚举值属性
    public SortKeys NameSortKey => SortKeys.Name;
    public SortKeys LastPlaySortKey => SortKeys.LastPlay;
    public SortKeys DeveloperSortKey => SortKeys.Developer;
    public SortKeys RatingSortKey => SortKeys.Rating;
    public SortKeys ReleaseDateSortKey => SortKeys.ReleaseDate;
    public SortKeys LastFetchInfoTimeSortKey => SortKeys.LastFetchInfoTime;
    public SortKeys AddTimeSortKey => SortKeys.AddTime;
    public SortKeys PathSortKey => SortKeys.Path;

    [ObservableProperty] private SortKeys _currentSortKey = SortKeys.Name;
    [ObservableProperty] private bool _gameSortDescending = true;
    
    [ObservableProperty] private GalgameSourceSortKeys _currentFolderSortKey = GalgameSourceSortKeys.Name;
    [ObservableProperty] private bool _folderSortDescending = true;
    // 为XAML绑定添加文件夹排序的静态枚举值属性
    public GalgameSourceSortKeys LibraryNameSortKey => GalgameSourceSortKeys.Name;
    public GalgameSourceSortKeys LibraryLastPlayedSortKey => GalgameSourceSortKeys.LastPlay;
    public GalgameSourceSortKeys LibraryPathSortKey => GalgameSourceSortKeys.Path;
    public GalgameSourceSortKeys LibrarySourceTypeSortKey => GalgameSourceSortKeys.SourceType;
    public GalgameSourceSortKeys LibraryGalgameCountSortKey => GalgameSourceSortKeys.GalgameCount;
    
    [RelayCommand]
    private void Sort(SortKeys sortKey)
    {
        // 如果点击当前排序键，则切换排序方向
        if (CurrentSortKey == sortKey)
        {
            GameSortDescending = !GameSortDescending;
        }
        else
        {
            CurrentSortKey = sortKey;
        }
        
        ApplySorting();
    }

    [RelayCommand]
    private void SortLibrary(GalgameSourceSortKeys sortKey)
    {
        // 如果点击当前排序键，则切换排序方向
        if (CurrentFolderSortKey == sortKey)
        {
            FolderSortDescending = !FolderSortDescending;
        }
        else
        {
            CurrentFolderSortKey = sortKey;
        }
        
        ApplySorting();
    }
    
    [RelayCommand]
    private void ApplySorting()
    {
        // 应用游戏排序 
        Galgames.SortDescriptions.Clear();
        SortDirection gameDirection = GameSortDescending ? SortDirection.Descending : SortDirection.Ascending;

        switch (CurrentSortKey)
        {
            case SortKeys.Name:
                Galgames.SortDescriptions.Add(new SortDescription(nameof(Galgame.Name), gameDirection));
                break;
            case SortKeys.LastPlay:
                Galgames.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastPlayTime), gameDirection));
                break;
            case SortKeys.Developer:
                Galgames.SortDescriptions.Add(new SortDescription(nameof(Galgame.Developer), gameDirection));
                break;
            case SortKeys.Rating:
                Galgames.SortDescriptions.Add(new SortDescription(nameof(Galgame.Rating), gameDirection));
                break;
            case SortKeys.ReleaseDate:
                Galgames.SortDescriptions.Add(new SortDescription(nameof(Galgame.ReleaseDate), gameDirection));
                break;
            case SortKeys.AddTime:
                Galgames.SortDescriptions.Add(new SortDescription(nameof(Galgame.AddTime), gameDirection));
                break;
            case SortKeys.Path:
                Galgames.SortDescriptions.Add(new SortDescription(nameof(Galgame.LocalPath), gameDirection));
                break;
        }

        // 应用文件夹排序逻辑
        if (Source.Count > 0)
        {
            List<IDisplayableGameObject> sorted = Source.Cast<IDisplayableGameObject>().ToList();
            sorted.Sort((x, y) =>
            {
                if (x is GalgameSourceBase sx && y is GalgameSourceBase sy)
                {
                    // VirtualSource 类型的源始终排在第一位
                    if (sx.SourceType == GalgameSourceType.Virtual && sy.SourceType != GalgameSourceType.Virtual)
                        return -1;
                    if (sx.SourceType != GalgameSourceType.Virtual && sy.SourceType == GalgameSourceType.Virtual)
                        return 1;
                    
                    // 如果都是或都不是 VirtualSource，则按照原有排序逻辑
                    var result = CurrentFolderSortKey switch
                    {
                        GalgameSourceSortKeys.Name => string.Compare(sx.Name, sy.Name, StringComparison.CurrentCultureIgnoreCase),
                        GalgameSourceSortKeys.LastPlay => DateTime.Compare(sx.LastPlayed, sy.LastPlayed),
                        GalgameSourceSortKeys.Path => string.Compare(sx.Path, sy.Path, StringComparison.CurrentCultureIgnoreCase),
                        GalgameSourceSortKeys.SourceType => sx.SourceType.CompareTo(sy.SourceType),
                        GalgameSourceSortKeys.GalgameCount => sx.Galgames.Count.CompareTo(sy.Galgames.Count),
                        _ => 0
                    };
                    return FolderSortDescending ? -result : result;
                }
                return 0;
            });
            Source.Clear();
            foreach (IDisplayableGameObject item in sorted)
                Source.Add(item);
        }
        
        _ = Task.Run(async () =>
        {
            await settingsService.SaveSettingAsync(KeyValues.LibrarySortKey, (int)CurrentSortKey);
            await settingsService.SaveSettingAsync(KeyValues.LibraryGameSortDescending, GameSortDescending);

            await settingsService.SaveSettingAsync(KeyValues.LibraryFolderSortKey, (int)CurrentFolderSortKey);
            await settingsService.SaveSettingAsync(KeyValues.LibraryFolderSortDescending, FolderSortDescending);
        });
    }

    #endregion
}
