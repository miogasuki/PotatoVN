using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
using CommunityToolkit.WinUI.Controls;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Filters;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

// ReSharper disable CollectionNeverQueried.Global

namespace GalgameManager.ViewModels;

public partial class HomeViewModel : ObservableObject, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    private readonly IInfoService _infoService;
    private readonly IBgTaskService _bgTaskService;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Stretch _stretch;
    [ObservableProperty] private bool _fixHorizontalPicture; // 是否修复横向图片（截断为标准的长方形）
    [ObservableProperty] private bool _displayPlayTypePolygon = true; // 是否显示游玩状态的小三角形
    [ObservableProperty] private bool _displayVirtualGame; //是否显示虚拟游戏
    [ObservableProperty] private bool _specialDisplayVirtualGame; //是否特殊显示虚拟游戏（降低透明度）

    #region UI
    public readonly string PlayStatus = "HomePage_PlayStatus".GetLocalized();
    private readonly string _uiSearch = "Search".GetLocalized();
    private readonly string _batchSelectAllLabel = "HomePage_SelectAll".GetLocalized();
    private readonly string _batchSelectNoneLabel = "HomePage_SelectNone".GetLocalized();
    public readonly string BatchDownloadLabel = "HomePage_Download".GetLocalized();
    public readonly string BatchRemoveLabel = "HomePage_Remove".GetLocalized();
    #endregion

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsItemClickEnabled))]
    [NotifyPropertyChangedFor(nameof(CanDragItems))]
    [NotifyPropertyChangedFor(nameof(CanReorderItems))]
    [NotifyPropertyChangedFor(nameof(GridSelectionMode))]
    [NotifyPropertyChangedFor(nameof(IsMultiSelectCheckBoxEnabled))]
    [NotifyPropertyChangedFor(nameof(IsAllSelected))]
    [NotifyPropertyChangedFor(nameof(BatchSelectLabel))]
    private bool _isBatchMode;
    public bool IsItemClickEnabled => !IsBatchMode;
    public bool CanDragItems => !IsBatchMode;
    public bool CanReorderItems => !IsBatchMode;
    public ListViewSelectionMode GridSelectionMode =>
        IsBatchMode ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    public bool IsMultiSelectCheckBoxEnabled => IsBatchMode;
    public bool IsAllSelected => IsBatchMode && Source.Count > 0 && SelectedGalgamesCount == Source.Count;
    public string BatchSelectLabel => IsAllSelected ? _batchSelectNoneLabel : _batchSelectAllLabel;

    /// <summary>
    /// 一定要有ObservableProperty，不然切换页面后不会更新
    /// </summary>
    [ObservableProperty] private AdvancedCollectionView _source = new(new List<Galgame>(), true);

    private readonly HashSet<Galgame> _selectedGalgames = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BatchChangePlayStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchDownloadInfoCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchRemoveCommand))]
    [NotifyPropertyChangedFor(nameof(HasBatchSelection))]
    [NotifyPropertyChangedFor(nameof(IsAllSelected))]
    [NotifyPropertyChangedFor(nameof(BatchSelectLabel))]
    private int _selectedGalgamesCount;

    public bool HasBatchSelection => SelectedGalgamesCount > 0;


    public HomeViewModel(INavigationService navigationService, IGalgameCollectionService dataCollectionService,
        ILocalSettingsService localSettingsService, IFilterService filterService, IInfoService infoService,
        IBgTaskService bgTaskService)
    {
        _navigationService = navigationService;
        _galgameService = (GalgameCollectionService)dataCollectionService;
        _localSettingsService = localSettingsService;
        _filterService = filterService;
        _infoService = infoService;
        _bgTaskService = bgTaskService;
    }

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            IsBatchMode = false;
            SearchTitle = SearchKey == string.Empty ? _uiSearch : _uiSearch + " ●";
            Source.Source = _galgameService.Galgames;
            Filters = _filterService.GetFilters();

            //Read Settings
            Stretch = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture)
                ? Stretch.UniformToFill : Stretch.Uniform;
            FixHorizontalPicture = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture);
            DisplayPlayTypePolygon = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon);
            DisplayVirtualGame = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayVirtualGame);
            SpecialDisplayVirtualGame = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SpecialDisplayVirtualGame);
            KeepFilters = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters);
            GameToOpacityConverter.SpecialDisplayVirtualGame = SpecialDisplayVirtualGame;

            PrimaryKey = (SortKeys)_localSettingsService.ReadSettingAsync<int>(KeyValues.PrimarySortKey).Result;
            IsPrimaryDescending = _localSettingsService.ReadSettingAsync<bool>(KeyValues.PrimarySortDescending).Result;

            SecondaryKey = (SortKeys)_localSettingsService.ReadSettingAsync<int>(KeyValues.SecondarySortKey).Result;
            IsSecondaryDescending = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SecondarySortDescending).Result;

            var customOrderList = _localSettingsService
                                 .ReadSettingAsync<List<string>>(KeyValues.CustomSortOrder, true).Result
                                  ?? [];
            ApplySort(customOrderList);

            //Add Event
            Filters.CollectionChanged += UpdateFilterPanelDisplay;
            _galgameService.GalgameLoadedEvent += OnGalgameLoadedEvent;
            _galgameService.GalgameChangedEvent += UpdateGalgame;
            _galgameService.PhrasedEvent += OnGalgameServicePhrased;
            _localSettingsService.OnSettingChanged += OnSettingChanged;
            _galgameService.Galgames.CollectionChanged += OnGalgamesCollectionChanged;
            Source.Filter = g =>
            {
                if (g is Galgame game && _filterService.ApplyFilters(game))
                {
                    return SearchKey.IsNullOrEmpty() || game.ApplySearchKey(SearchKey);
                }

                return false;
            };
            Source.Refresh();
            UpdateFilterPanelDisplay(null, null!);
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Oops, something went wrong", e);
        }
    }

    private void OnSettingChanged(string key, object? value)
    {
        switch (key)
        {
            case KeyValues.DisplayVirtualGame:
                DisplayVirtualGame = value is true;
                break;
        }
    }

    public async void OnNavigatedFrom()
    {
        try
        {
            await Task.Delay(200); //等待动画结束
            if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters) == false)
                _filterService.ClearFilters();
            _galgameService.PhrasedEvent -= OnGalgameServicePhrased;
            _galgameService.GalgameChangedEvent -= UpdateGalgame;
            _galgameService.GalgameLoadedEvent -= OnGalgameLoadedEvent;
            Filters.CollectionChanged -= UpdateFilterPanelDisplay;
            _galgameService.Galgames.CollectionChanged -= OnGalgamesCollectionChanged;
            _localSettingsService.OnSettingChanged -= OnSettingChanged;
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Oops, something went wrong", e);
        }
    }

    partial void OnIsBatchModeChanged(bool value)
    {
        if (!value)
        {
            ResetBatchSelection();
        }
    }

    private void OnGalgamesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyBatchSelectionStateChanged();
    }

    private void NotifyBatchSelectionStateChanged()
    {
        if (!IsBatchMode) return;
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(BatchSelectLabel));
    }

    private void ResetBatchSelection()
    {
        _selectedGalgames.Clear();
        SelectedGalgamesCount = 0;
    }

    [RelayCommand]
    private void ItemClick(Galgame? clickedItem)
    {
        if (clickedItem == null) return;
        NavigationHelper.NavigateToGalgamePage(_navigationService, new GalgamePageParameter { Galgame = clickedItem });
    }

    #region DRAG_AND_DROP

    [ObservableProperty] private bool _displayDragArea;

    public async void Grid_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();
            switch (items.Count)
            {
                case <= 0:
                    return;
                // 限制只能拖入一个项目
                case > 1:
                    _infoService.Info(InfoBarSeverity.Error, "HomePage_Drop_TooManyItems".GetLocalized());
                    break;
                default:
                    {
                        // 只处理单个项目
                        IStorageItem storageItem = items[0];
                        if (storageItem is StorageFile file &&
                            (file.FileType.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                             file.FileType.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
                        {
                            var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                            _ = AddGalgameInternal(folder);
                        }
                        else if (storageItem is StorageFolder folder)
                        {
                            _ = AddGalgameInternal(folder.Path);
                        }
                        else
                        {
                            _infoService.Info(InfoBarSeverity.Error, "HomePage_Drop_InvalidItem".GetLocalized());
                        }

                        break;
                    }
            }

            DisplayDragArea = false;
        }
        catch (Exception ex)
        {
            _infoService.DeveloperEvent(e: ex);
        }
    }

    public void Grid_DragEnter(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Link;
        DisplayDragArea = true;
    }

    public void Grid_DragLeave(object sender, DragEventArgs e)
    {
        DisplayDragArea = false;
    }

    #endregion

    #region FILTER
    [ObservableProperty] private string _uiFilter = string.Empty; //过滤器在AppBar上的文本
    [ObservableProperty] private bool _keepFilters; //是否保留过滤器
    [ObservableProperty] private string _filterInputText = string.Empty; //过滤器输入框的文本
    public ObservableCollection<FilterBase> Filters = null!;
    public readonly ObservableCollection<FilterBase> FilterInputSuggestions = new();

    private void UpdateFilterPanelDisplay(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var hasActiveFilters = Filters.Count > 0 && (DisplayVirtualGame || Filters.Any(f => !(f is VirtualGameFilter)));
        UiFilter = "HomePage_Filter".GetLocalized() + (hasActiveFilters ? " ●" : string.Empty);
        RefreshFilterAndSelection();
    }

    [RelayCommand]
    private void FilterRemoved(object args)
    {
        if (args is FilterBase filter)
        {
            _filterService.RemoveFilter(filter);

            // 如果删除的是虚拟游戏过滤器，同步更新DisplayVirtualGame属性
            if (filter is VirtualGameFilter)
            {
                DisplayVirtualGame = false;
            }
        }
    }

    [RelayCommand]
    private async Task FilterInputTextChange(AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        if (FilterInputText == string.Empty)
        {
            FilterInputSuggestions.Clear();
            return;
        }
        List<FilterBase> result = await _filterService.SearchFilters(FilterInputText);
        FilterInputSuggestions.Clear();
        foreach (FilterBase filter in result)
            FilterInputSuggestions.Add(filter);
    }

    [RelayCommand]
    private async Task FilterInputTokenItemAdding(TokenItemAddingEventArgs args)
    {
        var i = args.Item;
        var t = args.TokenText;
        args.Cancel = true;
        args.Item = null;
        if (i is FilterBase filter)
            _filterService.AddFilter(filter);
        else if (string.IsNullOrEmpty(t) == false)
        {
            List<FilterBase> result = await _filterService.SearchFilters(t);
            if (result.Count > 1)
                _filterService.AddFilter(result[0]);
            else
                _infoService.Info(InfoBarSeverity.Error, msg: "HomePage_Filter_Not_Found".GetLocalized());
        }

    }

    [RelayCommand]
    private void OnFilterFlyoutOpening(object arg)
    {
        UpdateFilterPanelDisplay(null, null!);
    }

    partial void OnKeepFiltersChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.KeepFilters, value);

    #endregion

    #region SEARCH
    [ObservableProperty] private string _searchKey = string.Empty;
    [ObservableProperty] private string _searchTitle = string.Empty;
    [ObservableProperty]
    private GalgameSearchSuggestionsProvider _galgameSearchSuggestionsProvider = new();

    [RelayCommand]
    private void Search(string searchKey)
    {
        SearchTitle = searchKey == string.Empty ? _uiSearch : _uiSearch + " ●";
        RefreshFilterAndSelection();
    }

    private void RefreshFilterAndSelection()
    {
        Source.RefreshFilter();
        NotifyBatchSelectionStateChanged();
    }

    #endregion

    #region BATCH_MANAGE

    [RelayCommand]
    private void ToggleBatchManageState() => IsBatchMode = !IsBatchMode;

    [RelayCommand]
    private void MainGridViewSelectionChanged(SelectionChangedEventArgs args)
    {
        if (!IsBatchMode) return;
        foreach (Galgame game in args.AddedItems.OfType<Galgame>())
            _selectedGalgames.Add(game);
        foreach (Galgame game in args.RemovedItems.OfType<Galgame>())
            _selectedGalgames.Remove(game);
        SelectedGalgamesCount = _selectedGalgames.Count;
    }

    [RelayCommand(CanExecute = nameof(HasBatchSelection))]
    private async Task BatchChangePlayStatus(string playTypeString)
    {
        if (!Enum.TryParse(playTypeString, out PlayType playType)) return;
        List<Galgame> selectedGalgames = GetSelectedGalgamesInViewOrder();
        if (selectedGalgames.Count == 0) return;

        var title = $"{PlayStatus} - {playType.GetLocalized()}";
        BatchChangePlayStatusDialog dialog = new(selectedGalgames, title, playType);
        dialog.Resources["ContentDialogMinWidth"] = App.MainWindow!.Bounds.Width * 0.4;
        await dialog.ShowAsync();
        if (dialog.Canceled) return;

        foreach (Galgame game in selectedGalgames)
        {
            game.PlayType = PlayType.None; //确保分类事件能被触发
            game.PlayType = dialog.SelectedPlayType;
            game.MyRate = dialog.SelectedRate;
            game.PrivateComment = dialog.PrivateComment;

            if (dialog.UploadToBgm)
            {
                _infoService.Info(InfoBarSeverity.Informational,
                    msg: "HomePage_UploadingToBgm".GetLocalized(),
                    displayTimeMs: 1000 * 10);
                (GalStatusSyncResult, string) result = await _galgameService.UploadPlayStatusAsync(game, RssType.Bangumi);
                _infoService.Info(result.Item1.ToInfoBarSeverity(), result.Item2);
                await Task.Delay(Random.Shared.Next(300, 801));
            }

            if (dialog.UploadToVndb)
            {
                _infoService.Info(InfoBarSeverity.Informational,
                    msg: "HomePage_UploadingToVndb".GetLocalized(),
                    displayTimeMs: 1000 * 10);
                (GalStatusSyncResult, string) result = await _galgameService.UploadPlayStatusAsync(game, RssType.Vndb);
                _infoService.Info(result.Item1.ToInfoBarSeverity(), result.Item2);
                await Task.Delay(Random.Shared.Next(300, 801));
            }

            await _galgameService.SaveGalgameAsync(game);
        }
    }

    private static readonly List<GetGalgameInfoFromRssTask> BatchRssTasks = [];

    [RelayCommand(CanExecute = nameof(HasBatchSelection))]
    private async Task BatchDownloadInfo()
    {
        if (_selectedGalgames.Count == 0) return;
        if (!await ConfirmBatchActionAsync(BatchDownloadLabel)) return;

        SelectGameInfoToFetchDialog selectInfoDialog = new(showIncludingSubSourcesCheckBox: false);
        await selectInfoDialog.ShowAsync();
        if (selectInfoDialog.Canceled) return;

        GameParseType selectedParseTypes = selectInfoDialog.SelectedParseTypes;
        var groups = _selectedGalgames
            .Select(game => new { Game = game, Source = game.Sources.FirstOrDefault() })
            .Where(item => item.Source is not null)
            .GroupBy(item => item.Source!);

        foreach (var group in groups)
        {
            var task = new GetGalgameInfoFromRssTask(group.Key, selectedParseTypes,
                group.Select(item => item.Game).ToList());
            BatchRssTasks.Add(task);
            _ = _bgTaskService.AddBgTask(task);
        }
    }

    [RelayCommand(CanExecute = nameof(HasBatchSelection))]
    private async Task BatchRemove()
    {
        if (_selectedGalgames.Count == 0) return;
        var title = "HomePage_Remove_Title".GetLocalized();
        var message = "HomePage_BatchRemove_Message".GetLocalized(_selectedGalgames.Count);
        if (!await ConfirmBatchActionAsync(title, message)) return;

        foreach (Galgame game in _selectedGalgames.ToList())
        {
            await _galgameService.RemoveGalgame(game);
        }
        ResetBatchSelection();
    }

    #endregion

    #region SORT

    // 为XAML绑定添加静态排序键属性
    [ObservableProperty] private SortKeys _primaryKey = SortKeys.LastPlay;
    [ObservableProperty] private bool _isPrimaryDescending;
    [ObservableProperty] private SortKeys _secondaryKey = SortKeys.Developer;
    [ObservableProperty] private bool _isSecondaryDescending;

    private bool _suppressSort;

    [RelayCommand]
    private void SetPrimaryKey(string key)
    {
        if (Enum.TryParse(key, out SortKeys sortKey))
        {
            PrimaryKey = sortKey;
            ApplySort();
        }
    }

    [RelayCommand]
    private void SetSecondaryKey(string key)
    {
        if (Enum.TryParse(key, out SortKeys sortKey))
        {
            SecondaryKey = sortKey;
            ApplySort();
        }
    }

    public void EnterCustomSortMode()
    {
        if (PrimaryKey == SortKeys.Custom && SecondaryKey == SortKeys.Custom) return;
        _suppressSort = true;
        try
        {
            PrimaryKey = SortKeys.Custom;
            SecondaryKey = SortKeys.Custom;
            IsPrimaryDescending = false;
            IsSecondaryDescending = false;
            Source.SortDescriptions.Clear();
            Source.RefreshSorting();
            SaveSortSettings();
        }
        finally
        {
            _suppressSort = false;
        }
    }

    private void ApplyCustomOrder(List<string> customOrder)
    {
        Source.SortDescriptions.Clear();
        Source.RefreshSorting();
        var indexMap = customOrder
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        ObservableCollection<Galgame> collection = _galgameService.Galgames;

        List<Galgame> inOrderItems = new();
        List<Galgame> missingItems = new();
        foreach (var gal in collection)
        {
            if (indexMap.ContainsKey(gal.Uuid.ToString()))
                inOrderItems.Add(gal);
            else
                missingItems.Add(gal);
        }
        inOrderItems
            .Sort((a, b) =>
                indexMap[a.Uuid.ToString()].CompareTo(indexMap[b.Uuid.ToString()]));
        List<Galgame> targetList = inOrderItems.Concat(missingItems).ToList();
        for (var i = 0; i < targetList.Count; i++)
        {
            Galgame targetItem = targetList[i];
            var oldIndex = collection.IndexOf(targetItem);
            if (oldIndex != i && oldIndex != -1)
            {
                collection.Move(oldIndex, i);
            }
        }
    }

    [RelayCommand]
    private void ApplySort(List<string>? customOrder = null)
    {
        if (_suppressSort) return;
        SaveSortSettings();

        // 清除现有排序
        Source.SortDescriptions.Clear();

        if (PrimaryKey == SortKeys.Custom && SecondaryKey == SortKeys.Custom)
        {
            Source.RefreshSorting();
            if (customOrder is not null) ApplyCustomOrder(customOrder);
            return;
        }

        // 应用主排序键
        SortDirection primaryDirection = IsPrimaryDescending ? SortDirection.Descending : SortDirection.Ascending;
        switch (PrimaryKey)
        {
            case SortKeys.Name:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Name),
                    primaryDirection, StringComparer.CurrentCultureIgnoreCase));
                break;
            case SortKeys.Developer:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Developer),
                    primaryDirection, StringComparer.Ordinal));
                break;
            case SortKeys.Rating:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Rating),
                    primaryDirection));
                break;
            case SortKeys.LastPlay:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastPlayTime),
                    primaryDirection));
                break;
            case SortKeys.ReleaseDate:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.ReleaseDate),
                    primaryDirection));
                break;
            case SortKeys.LastFetchInfoTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastFetchInfoTime),
                    primaryDirection));
                break;
            case SortKeys.AddTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.AddTime),
                    primaryDirection));
                break;
        }

        // 应用次要排序键
        SortDirection secondaryDirection = IsSecondaryDescending ? SortDirection.Descending : SortDirection.Ascending;
        switch (SecondaryKey)
        {
            case SortKeys.Name:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Name),
                    secondaryDirection, StringComparer.CurrentCultureIgnoreCase));
                break;
            case SortKeys.Developer:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Developer),
                    secondaryDirection, StringComparer.Ordinal));
                break;
            case SortKeys.Rating:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.Rating),
                    secondaryDirection));
                break;
            case SortKeys.LastPlay:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastPlayTime),
                    secondaryDirection));
                break;
            case SortKeys.ReleaseDate:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.ReleaseDate),
                    secondaryDirection));
                break;
            case SortKeys.LastFetchInfoTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.LastFetchInfoTime),
                    secondaryDirection));
                break;
            case SortKeys.AddTime:
                Source.SortDescriptions.Add(new SortDescription(nameof(Galgame.AddTime),
                    secondaryDirection));
                break;
        }

        Source.RefreshSorting();
    }

    // 新增方法，统一保存排序相关设置
    private void SaveSortSettings()
    {
        _localSettingsService.SaveSettingAsync(KeyValues.PrimarySortKey, (int)PrimaryKey);
        _localSettingsService.SaveSettingAsync(KeyValues.PrimarySortDescending, IsPrimaryDescending);
        _localSettingsService.SaveSettingAsync(KeyValues.SecondarySortKey, (int)SecondaryKey);
        _localSettingsService.SaveSettingAsync(KeyValues.SecondarySortDescending, IsSecondaryDescending);
    }

    public async void SaveCustomSortOrder()
    {
        try
        {
            List<string> customSortOrder = Source.Cast<Galgame>()
                .Select(g => g.Uuid.ToString())
                .ToList();
            await _localSettingsService.SaveSettingAsync(KeyValues.CustomSortOrder, customSortOrder, true);
        }
        catch (Exception ex)
        {
            _infoService.DeveloperEvent(e: ex);
        }
    }

    #endregion

    /// <summary>
    /// 添加Galgame
    /// </summary>
    /// <param name="path">游戏文件夹路径</param>
    /// <param name="isVirtual">添加非本机游戏则设为true</param>
    private async Task AddGalgameInternal(string path, bool isVirtual = false)
    {
        //TODO
        IsPhrasing = true;
        InfoBarSeverity infoBarSeverity;
        string msg;
        try
        {
            Galgame tmp = await _galgameService.AddGameAsync(
                isVirtual ? GalgameSourceType.Virtual : GalgameSourceType.LocalFolder, path, true);
            infoBarSeverity = tmp.IsIdsEmpty() ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
            msg = tmp.IsIdsEmpty()
                ? "AddGalgameResult_NotFoundInRss".GetLocalized()
                : "AddGalgameResult_Success".GetLocalized();
        }
        catch (Exception e)
        {
            infoBarSeverity = InfoBarSeverity.Error;
            msg = e is PvnException ? e.Message : e.ToString();
        }

        IsPhrasing = false;
        _infoService.Info(infoBarSeverity, msg: msg);
    }

    private void OnGalgameServicePhrased() => IsPhrasing = false;

    // private void OnGalgameLoadedEvent() => Source.Source = _galgameService.Galgames;
    private void OnGalgameLoadedEvent()
    {
        Source.Source = _galgameService.Galgames;
        Source.Refresh();
        NotifyBatchSelectionStateChanged();
    }

    private void UpdateGalgame(Galgame game)
    {
        //通过Remove和Add来刷新某个具体的Item
        UiThreadInvokeHelper.Invoke(() =>
        {
            Source.Remove(game);
            Source.Add(game);
        });
    }

    [RelayCommand]
    private async Task AddGalgame()
    {
        try
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            openPicker.FileTypeFilter.Add(".bat");
            openPicker.FileTypeFilter.Add(".EXE");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is not null)
            {
                var folder = file.Path[..file.Path.LastIndexOf('\\')];
                await AddGalgameInternal(folder);
            }
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.ToString());
        }
    }

    [RelayCommand]
    private async Task AddVirtualGame()
    {
        BasicDialog dialog = new("GalgamePage_AddVirtualGame".GetLocalized(), inputBox: true,
            inputBoxPlaceHolder: "GalgamePage_AddVirtualGame_PlaceHolder".GetLocalized(), minWidth: 200);
        await dialog.ShowAsync();
        if (!dialog.PrimaryButtonClicked) return;
        await AddGalgameInternal(dialog.InputText, true);
    }

    #region MenuFlyout
    [ObservableProperty] private Galgame? _currentContextGame; // 当前右键菜单上下文游戏对象

    [RelayCommand]
    private async Task GalFlyOutDelete(Galgame? galgame)
    {
        if (galgame == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default,
            Title = "HomePage_Remove_Title".GetLocalized(),
            Content = "HomePage_Remove_Message".GetLocalized(),
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized()
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(galgame);
        };

        await dialog.ShowAsync();
    }

    [RelayCommand]
    private void GalFlyOutEdit(Galgame? galgame)
    {
        if (galgame == null) return;
        // 在主线程中执行导航，修复appbarbutton描述文字延迟显示的问题
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
            _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame)
        );
    }

    [RelayCommand]
    private async Task GalFlyOutGetInfoFromRss(Galgame? galgame)
    {
        if (galgame == null) return;
        IsPhrasing = true;
        await _galgameService.ParseGalInfoAsync(galgame);
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

        await _galgameService.SaveGalgameAsync(CurrentContextGame);
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

        ChangePlayStatusDialog dialog = new(game);
        await dialog.ShowAsync();

        if (!dialog.Canceled)
        {
            await _galgameService.SaveGalgameAsync(game);
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

    [RelayCommand]
    private async Task OpenGameInExplorer(Galgame? game)
    {
        if (game == null) return;
        StorageFolder? folder = await StorageFolder.GetFolderFromPathAsync(game.LocalPath);
        await Launcher.LaunchFolderAsync(folder);
    }

    #endregion

    private async Task<bool> ConfirmBatchActionAsync(string title, string? message = null)
    {
        if (_selectedGalgames.Count == 0) return false;

        List<Galgame> selectedGalgames = GetSelectedGalgamesInViewOrder();
        BatchConfirmDialog dialog = new(selectedGalgames, title, message)
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot
        };
        dialog.Resources["ContentDialogMinWidth"] = App.MainWindow.Bounds.Width * 0.4;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private List<Galgame> GetSelectedGalgamesInViewOrder()
    {
        List<Galgame> selectedGalgames = [.. Source.OfType<Galgame>().Where(game => _selectedGalgames.Contains(game))];
        return selectedGalgames.Count > 0 ? selectedGalgames : [.. _selectedGalgames];
    }

    partial void OnFixHorizontalPictureChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.FixHorizontalPicture, value);
        Stretch = value ? Stretch.UniformToFill : Stretch.Uniform;
        if (value == false)
            DisplayPlayTypePolygon = false;
    }

    partial void OnDisplayPlayTypePolygonChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.DisplayPlayTypePolygon, value);

    partial void OnDisplayVirtualGameChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.DisplayVirtualGame, value);

    partial void OnSpecialDisplayVirtualGameChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.SpecialDisplayVirtualGame, value);
        GameToOpacityConverter.SpecialDisplayVirtualGame = value;
        Source.Refresh();
    }
}

public class GalgameSearchSuggestionsProvider : ISearchSuggestionsProvider
{
    private readonly GalgameCollectionService _galgameCollectionService;
    private readonly bool _searchName, _searchDeveloper, _searchTags, _searchChineseName, _searchOriginalName;

    public GalgameSearchSuggestionsProvider(bool searchName = true, bool searchDeveloper = true, bool searchTags = true, bool searchChineseName = true, bool searchOriginalName = true)
    {
        _searchName = searchName;
        _searchDeveloper = searchDeveloper;
        _searchTags = searchTags;
        _searchChineseName = searchChineseName;
        _searchOriginalName = searchOriginalName;
        _galgameCollectionService = (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!;
    }
    public async Task<IEnumerable<string>?> GetSearchSuggestionsAsync(string key)
    {
        return await _galgameCollectionService.GetSearchSuggestions(key, _searchName, _searchDeveloper, _searchTags, _searchChineseName, _searchOriginalName);
    }
}
