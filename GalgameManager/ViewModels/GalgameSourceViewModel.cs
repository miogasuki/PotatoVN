using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace GalgameManager.ViewModels;

public partial class GalgameSourceViewModel : ObservableObject, INavigationAware
{
    private readonly IGalgameSourceCollectionService _sourceService;
    private readonly GalgameCollectionService _galgameService;
    private readonly IBgTaskService _bgTaskService;
    private readonly IInfoService _infoService;
    private readonly INavigationService _navigationService;
    private readonly ILocalSettingsService _settingsService;
    
    private GalgameSourceBase? _item;
    public ObservableCollection<GalgameAndPath> Galgames { get; } = new();
    public List<RssType> RssTypes { get; } = new(){RssType.Bangumi, RssType.Vndb, RssType.Ymgal, RssType.Cngal, RssType.Mixed, RssType.None};
    private readonly List<Galgame> _selectedGalgames = new();
    private UnpackGameTask? _unpackGameTask;
    
    [ObservableProperty] private bool _isUnpacking;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _progressMsg = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetInfoFromRssCommand), nameof(ScanAllCommand))]
    private bool _canExecute; //是否正在运行命令
    [ObservableProperty] private bool _logExists; //是否存在日志文件

    [ObservableProperty] private double _titleMaxWidth = 200;
    [ObservableProperty] private double _gameListHeight;
    [ObservableProperty] private bool _gameListExpend;
    private double _commandBarWidth;
    private double _pageWidth;

    [ObservableProperty] private bool _includeSubSources;
    
    #region SORTING
    // 为XAML绑定添加静态枚举值属性
    public SortKeys NameSortKey => SortKeys.Name;
    public SortKeys LastPlaySortKey => SortKeys.LastPlay;
    public SortKeys DeveloperSortKey => SortKeys.Developer;
    public SortKeys RatingSortKey => SortKeys.Rating;
    public SortKeys ReleaseDateSortKey => SortKeys.ReleaseDate;
    public SortKeys LastFetchInfoTimeSortKey => SortKeys.LastFetchInfoTime;
    public SortKeys AddTimeSortKey => SortKeys.AddTime;

    [ObservableProperty] private SortKeys _currentSortKey = SortKeys.Name;
    [ObservableProperty] private bool _sortDescending = true;
    
    [RelayCommand]
    private void Sort(SortKeys sortKey)
    {
        // 如果点击当前排序键，则切换排序方向
        if (CurrentSortKey == sortKey)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            CurrentSortKey = sortKey;
        }
        
        ApplySorting();
    }
    
    [RelayCommand]
    private void ApplySorting()
    {
        // 创建一个新的AdvancedCollectionView来应用排序
        var sortedGames = new List<GalgameAndPath>(Galgames);

        // 根据当前排序键和方向应用排序
        Comparison<GalgameAndPath> comparison = (x, y) =>
        {
            int result = 0;
            switch (CurrentSortKey)
            {
                case SortKeys.Name:
                    result = string.Compare(x.Galgame.Name.Value, y.Galgame.Name.Value, StringComparison.CurrentCultureIgnoreCase);
                    break;
                case SortKeys.LastPlay:
                    result = DateTime.Compare(x.Galgame.LastPlayTime, y.Galgame.LastPlayTime);
                    break;
                case SortKeys.Developer:
                    result = string.Compare(x.Galgame.Developer, y.Galgame.Developer, StringComparison.CurrentCultureIgnoreCase);
                    break;
                case SortKeys.Rating:
                    result = x.Galgame.Rating.CompareTo(y.Galgame.Rating);
                    break;
                case SortKeys.ReleaseDate:
                    result = DateTime.Compare(x.Galgame.ReleaseDate, y.Galgame.ReleaseDate);
                    break;
                case SortKeys.AddTime:
                    result = DateTime.Compare(x.Galgame.AddTime, y.Galgame.AddTime);
                    break;
            }
            
            return SortDescending ? -result : result; // 如果是降序，则反转结果
        };

        // 应用排序
        sortedGames.Sort(comparison);
        
        // 更新集合
        Galgames.Clear();
        foreach (var game in sortedGames)
        {
            Galgames.Add(game);
        }

        // 保存排序设置到本地设置
        _ = Task.Run(async () =>
        {
            await _settingsService.SaveSettingAsync(KeyValues.LibrarySortKey, (int)CurrentSortKey);
            await _settingsService.SaveSettingAsync(KeyValues.LibrarySortDescending, SortDescending);
        });
    }
    #endregion

    #region UI_STRING

    [ObservableProperty] private string _uiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();
    [ObservableProperty] private bool _isDownloadFromNameVisible;

    public string ImagePathDes => Item?.ImagePath ?? "GalgameSourcePage_Setting_NoImage".GetLocalized();

    #endregion

    public GalgameSourceBase? Item
    {
        get => _item;

        private set
        {
            if (_item is not null)
            {
                _item.GalgamesChanged -= ReloadGalgameList;
                _item.PropertyChanged -= Save;
            }
            SetProperty(ref _item, value);
            if (value != null)
            {
                LoadGames();
                value.GalgamesChanged += ReloadGalgameList;
                value.PropertyChanged += Save;
            }
        }
    }

    public GalgameSourceViewModel(IGalgameSourceCollectionService dataCollectionService, 
        IGalgameCollectionService galgameService, IBgTaskService bgTaskService, IInfoService infoService, 
        INavigationService navigationService, ILocalSettingsService settingsService)
    {
        _sourceService = dataCollectionService;
        _galgameService = (GalgameCollectionService)galgameService;
        _bgTaskService = bgTaskService;
        _infoService = infoService;
        _navigationService = navigationService;
        _settingsService = settingsService;
    }

    private void LoadGames()
    {
        Galgames.Clear();
        if (_item == null) return;
        
        // 加载当前库中的游戏
        foreach (GalgameAndPath g in _item.Galgames)
        {
            Galgames.Add(g);
        }
        
        // 如果设置为包含子库，则递归加载所有子库中的游戏
        if (IncludeSubSources)
        {
            LoadSubSourceGames(_item);
        }
        
        // 应用排序
        ApplySorting();
    }

    private void LoadSubSourceGames(GalgameSourceBase source)
    {
        foreach (var subSource in source.SubSources)
        {
            foreach (GalgameAndPath g in subSource.Galgames)
            {
                if (!Galgames.Any(existing => existing.Galgame == g.Galgame))
                {
                    Galgames.Add(g);
                }
            }
            
            // 递归加载子库的子库
            LoadSubSourceGames(subSource);
        }
    }

    private void ReloadGalgameList(Galgame game, bool isDeleted)
    {
        if (_item == null) return;
        if (isDeleted && Galgames.FirstOrDefault(g => g.Galgame == game) is { } tmp)
            Galgames.Remove(tmp);
        else if (!isDeleted)
        {
            // 检查游戏是在当前库还是子库中
            var path = _item.GetPath(game);
            if (path != null)
            {
                Galgames.Add(new GalgameAndPath(game, path));
            }
            else if (IncludeSubSources)
            {
                // 递归检查子库
                CheckSubSourcesForGame(_item, game);
            }
        }
        // 更新UI
        OnPropertyChanged(nameof(Galgames));
        
        // 重新应用排序
        ApplySorting();
    }

    private bool CheckSubSourcesForGame(GalgameSourceBase source, Galgame game)
    {
        foreach (var subSource in source.SubSources)
        {
            var path = subSource.GetPath(game);
            if (path != null)
            {
                Galgames.Add(new GalgameAndPath(game, path));
                return true;
            }
            
            if (CheckSubSourcesForGame(subSource, game))
            {
                return true;
            }
        }
        return false;
    }

    public void OnNavigatedTo(object parameter)
    {
        IncludeSubSources = _settingsService.ReadSettingAsync<bool>(KeyValues.GalgameSourcePageShowSubSourceGames).Result;
        
        // 加载排序设置
        CurrentSortKey = (SortKeys)_settingsService.ReadSettingAsync<int>(KeyValues.LibrarySortKey).Result;
        SortDescending = _settingsService.ReadSettingAsync<bool>(KeyValues.LibrarySortDescending).Result;
        
        if (parameter is not string url) return;
        //TODO
        Item = _sourceService.GetGalgameSourceFromUrl(url);
        if (Item == null) return;
        
        _unpackGameTask = _bgTaskService.GetBgTask<UnpackGameTask>(Item.Url);
        if (_unpackGameTask != null)
        {
            _unpackGameTask.OnProgress += UpdateNotifyUnpack;
            UpdateNotifyUnpack(_unpackGameTask.CurrentProgress);
        }
        Update();
    }

    public void OnNavigatedFrom()
    {
        if (_unpackGameTask != null)
        {
            _unpackGameTask.OnProgress -= UpdateNotifyUnpack;
            _unpackGameTask.OnProgress -= HandelUnpackError;
        }
        Item = null; //确保监听注销
    }

    private void Update()
    {
        if(Item is null) return;
        CanExecute = !Item.IsRunning;
        IsUnpacking = _bgTaskService.GetBgTask<UnpackGameTask>(Item.Path)?.IsRunning ?? false;
        LogExists = FileHelper.Exists(Item.GetLogPath());
    }

    private void UpdateNotifyUnpack(Progress progress)
    {
        if(Item == null) return;
        Update();
        ProgressValue = (int)((double)progress.Current / progress.Total * 100);
        ProgressMsg = progress.Message;
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task GetInfoFromRss(object parameter)
    {
        if (Item == null) return;
        

        // 检查是否是 从游戏名下载信息 模式
        if (parameter is string isNameOnly && isNameOnly == "True")
        {
            // 清除目前存储的id信息
            foreach (var galgame in _selectedGalgames)
            {
                for (var i = 0; i < Galgame.PhraserNumber; i++)
                {
                    // 跳过potato
                    if (i == (int)RssType.PotatoVn)
                        continue;
                    galgame.Ids[i] = null;
                }
            }
        }

        if (_selectedGalgames.Count > 0)
        {
            var getGalgameInfoFromRss = new GetGalgameInfoFromRssTask(Item, _selectedGalgames);
            getGalgameInfoFromRss.OnProgress += progress =>
            {
                Update();
                _infoService.Info(progress.ToSeverity(), msg: progress.Message,
                    displayTimeMs: progress.ToSeverity() switch
                    {
                        InfoBarSeverity.Informational => 300000,
                        _ => 3000
                    });
            };
            _ = _bgTaskService.AddBgTask(getGalgameInfoFromRss);
            return;
        }

        // 没有选中任何游戏，获取当前库下所有游戏的信息

        // 创建确认对话框
        CheckBox includeSubfoldersCheckBox = new()
        {
            Content = "LibraryPage_GetInfoFromRss_IncludeSubfolders".GetLocalized(),
            IsChecked = true
        };

        StackPanel dialogContent = new()
        {
            Spacing = 10
        };
        dialogContent.Children.Add(new TextBlock { Text = "LibraryPage_GetInfoFromRss_Content".GetLocalized() });
        dialogContent.Children.Add(includeSubfoldersCheckBox);
        
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
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

        bool scanSubfolders = includeSubfoldersCheckBox.IsChecked ?? true;

        // 获取当前目录下所有库
        List<GalgameSourceBase> sources = new();
        sources.Add(Item);

        // 如果用户选择包含子文件夹，则添加所有子库
        if (scanSubfolders)
        {
            var allSources = _sourceService.GetGalgameSources();
            AddSubSources(Item, allSources);
        }

        // 对于这个列表，每个库都创建一个GetGalgameInfoFromRssTask，并加入到BgTaskService中
        foreach (GalgameSourceBase source in sources)
        {
            var getGalgameInfoFromRss = new GetGalgameInfoFromRssTask(source);
            getGalgameInfoFromRss.OnProgress += progress =>
            {
                Update();
                _infoService.Info(progress.ToSeverity(), msg: progress.Message,
                    displayTimeMs: progress.ToSeverity() switch
                    {
                        InfoBarSeverity.Informational => 300000,
                        _ => 3000
                    });
            };
            _ = _bgTaskService.AddBgTask(getGalgameInfoFromRss);
        }

        return;

        void AddSubSources(GalgameSourceBase parent, IEnumerable<GalgameSourceBase> allSources)
        {
            foreach (var source in allSources.Where(s => s.ParentSource == parent))
            {
                sources.Add(source);
                AddSubSources(source, allSources);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ScanAll()
    {
        // 和 LibraryViewModel 中的 ScanAll() 基本一致
        if (Item == null) return;
        // 创建确认对话框
        CheckBox includeSubfoldersCheckBox = new()
        {
            Content = "LibraryPage_ScanAll_IncludeSubfolders".GetLocalized(),
            IsChecked = true
        };

        StackPanel dialogContent = new()
        {
            Spacing = 10
        };
        dialogContent.Children.Add(new TextBlock { Text = "LibraryPage_ScanAll_Content".GetLocalized() });
        dialogContent.Children.Add(includeSubfoldersCheckBox);

        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
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

        bool scanSubfolders = includeSubfoldersCheckBox.IsChecked ?? true;

        // 获取当前目录下所有库
        List<GalgameSourceBase> sources = new();
        sources.Add(Item);

        // 如果用户选择包含子文件夹，则添加所有子库
        if (scanSubfolders)
        {
            var allSources = _sourceService.GetGalgameSources();
            AddSubSources(Item, allSources);
        }

        foreach (var source in sources)
        {
            Update();
            _sourceService.Scan(source);
            _infoService.Info(InfoBarSeverity.Success, msg: "LibraryPage_Scan_Success".GetLocalized(source.Name));
        }

        return;

        void AddSubSources(GalgameSourceBase parent, IEnumerable<GalgameSourceBase> allSources)
        {
            foreach (var source in allSources.Where(s => s.ParentSource == parent))
            {
                sources.Add(source);
                AddSubSources(source, allSources);
            }
        }
        
    }

    [RelayCommand(CanExecute = nameof(IsLocalFolder))]
    private async Task AddGalFromZip(string? passWord = null)
    {
        if (_item is not GalgameFolderSource) return;
        UnpackDialog dialog = new();
        await dialog.ShowAsync();
        StorageFile? file = dialog.StorageFile;

        if (file == null || _item == null) return;

        _unpackGameTask = new UnpackGameTask(file, Item!.Path, dialog.GameName, dialog.Password);
        _unpackGameTask.OnProgress += UpdateNotifyUnpack;
        _unpackGameTask.OnProgress += HandelUnpackError;
        _ = _bgTaskService.AddBgTask(_unpackGameTask);
    }
    
    [RelayCommand]
    private async Task SetImagePath(bool reset = false)
    {
        if (Item is null) return;
        if (reset)
            Item.ImagePath = null;
        else
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".png");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".bmp");
            openPicker.FileTypeFilter.Add(".webp");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file != null)
                Item.ImagePath = file.Path;
        }
        OnPropertyChanged(nameof(ImagePathDes));
    }

    private void Save(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        if (Item is null) return;
        _sourceService.Save(Item);
    }
    
    private bool IsLocalFolder()
    {
        return Item?.SourceType == GalgameSourceType.LocalFolder;
    }

    private void HandelUnpackError(Progress progress)
    {
        if(progress.ToSeverity() != InfoBarSeverity.Error) return;
        _infoService.Info(InfoBarSeverity.Error, msg:"GalgameFolder_UnpackGame_Error".GetLocalized());
    }

    [RelayCommand]
    private void OnSelectionChanged(object et)
    {
        SelectionChangedEventArgs e = (SelectionChangedEventArgs) et;
        foreach(GalgameAndPath g in e.AddedItems)
            _selectedGalgames.Add(g.Galgame);
        foreach (GalgameAndPath g in e.RemovedItems)
            _selectedGalgames.Remove(g.Galgame);
        UiDownloadInfo = _selectedGalgames.Count == 0
            ? "GalgameFolderPage_DownloadInfo".GetLocalized()
            : "GalgameFolderPage_DownloadSelectedInfo".GetLocalized();
        if (_selectedGalgames.Count != 0)
            IsDownloadFromNameVisible = true;
        else
            IsDownloadFromNameVisible = false;
    }

    private void UpdateTitleMaxWidth()
    {
        if (_pageWidth == 0 || _commandBarWidth == 0) return;
        TitleMaxWidth = Math.Max(_pageWidth - _commandBarWidth - 20, 0);
    }
    
    [RelayCommand]
    private void OnPageSizeChanged(SizeChangedEventArgs e)
    {
        _pageWidth = e.NewSize.Width;
        UpdateTitleMaxWidth();
        GameListHeight = Math.Max(e.NewSize.Height - 200, 0);
    }

    [RelayCommand]
    private void OnCommandBarSizeChanged(SizeChangedEventArgs e)
    {
        _commandBarWidth = e.NewSize.Width;
        UpdateTitleMaxWidth();
    }
    
    [RelayCommand]
    private async Task ViewLog()
    {
        if(Item is null) return;
        var path = Item.GetLogPath();
        if(FileHelper.Exists(path) == false) return; 
        await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(FileHelper.GetFullPath(path)));
    }

    [RelayCommand]
    private async Task DeleteSingleGame(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "GalgameSourcePage_Remove_Title".GetLocalized(),
            Content = string.Format("GalgameSourcePage_Remove_SingleGame".GetLocalized(), gameAndPath.Galgame.Name.Value),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _galgameService.RemoveGalgame(gameAndPath.Galgame);
            // 如果当前游戏在选中列表中，也要将其移除
            if (_selectedGalgames.Contains(gameAndPath.Galgame))
            {
                _selectedGalgames.Remove(gameAndPath.Galgame);
                // 更新UI状态
                if (_selectedGalgames.Count == 0)
                {
                    UiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();
                    IsDownloadFromNameVisible = false;
                }
            }
        }
    }

    [RelayCommand]
    private async Task DeleteGame()
    {
        if (_selectedGalgames.Count == 0) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : Microsoft.UI.Xaml.ElementTheme.Default,
            Title = "GalgameSourcePage_Remove_Title".GetLocalized(),
            Content = string.Format("GalgameSourcePage_Remove_Message".GetLocalized(), _selectedGalgames.Count),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            // 创建选中游戏的副本，避免在遍历过程中集合被修改
            List<Galgame> gamesToRemove = new(_selectedGalgames);
            foreach (var galgame in gamesToRemove)
            {
                await _galgameService.RemoveGalgame(galgame);
            }
            // 操作完成后清空选中集合
            _selectedGalgames.Clear();
            UiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();
            IsDownloadFromNameVisible = false;
        };
    
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private void EditGame(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        NavigationHelper.NavigateToGalgameSettingPage(_navigationService, gameAndPath.Galgame);
    }

    [RelayCommand]
    private void CopyGameName(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(gameAndPath.Galgame.Name.Value);
        Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    private void CopyGamePath(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(gameAndPath.Path);
        Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    private async Task OpenGameInExplorer(GalgameAndPath gameAndPath)
    {
        if (gameAndPath?.Galgame == null) return;
        var folder = await StorageFolder.GetFolderFromPathAsync(gameAndPath.Galgame.LocalPath);
        await Launcher.LaunchFolderAsync(folder);
    }

    partial void OnIncludeSubSourcesChanged(bool value)
    {
        if (_item != null)
        {
            _settingsService.SaveSettingAsync(KeyValues.GalgameSourcePageShowSubSourceGames, value);
            LoadGames();
        }
    }
}