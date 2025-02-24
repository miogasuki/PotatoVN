using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace GalgameManager.ViewModels;

public partial class GalgameSettingViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private Galgame _gal = null!;

    public List<RssType> RssTypes { get; }= new() { RssType.Bangumi, RssType.Vndb, RssType.Mixed, RssType.Ymgal, RssType.Cngal };

    private readonly GalgameCollectionService _galService;
    private readonly GalgameSourceCollectionService _sourceService;
    private readonly INavigationService _navigationService;
    private readonly IPvnService _pvnService;
    private readonly IInfoService _infoService;
    private readonly string[] _searchUrlList = new string[Galgame.PhraserNumber];
    [ObservableProperty] private string _searchUri = "";
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private RssType _selectedRss = RssType.None;
    [ObservableProperty] private string _lastFetchInfoStr = string.Empty;

    public GalgameSettingViewModel(IGalgameCollectionService galCollectionService, INavigationService navigationService,
        IPvnService pvnService, IInfoService infoService, IGalgameSourceCollectionService sourceService)
    {
        Gal = new Galgame();
        _galService = (GalgameCollectionService)galCollectionService;
        _sourceService = (GalgameSourceCollectionService)sourceService;
        _navigationService = navigationService;
        _pvnService = pvnService;
        _infoService = infoService;
        _searchUrlList[(int)RssType.Bangumi] = "https://bgm.tv/subject_search/";
        _searchUrlList[(int)RssType.Vndb] = "https://vndb.org/v/all?sq=";
        _searchUrlList[(int)RssType.Mixed] = "https://bgm.tv/subject_search/";
        _searchUrlList[(int)RssType.Ymgal] = "https://www.ymgal.games/search?type=ga&keyword=";
        _searchUrlList[(int)RssType.Cngal] = "https://www.cngal.org/search?Types=Game&Text=";
        SearchUri = _searchUrlList[(int)RssType.Vndb]; // default
    }

    public async void OnNavigatedFrom()
    {
        if (Gal.ImagePath.Value != Galgame.DefaultImagePath && !File.Exists(Gal.ImagePath.Value))
            Gal.ImagePath.Value = Galgame.DefaultImagePath;
        await _galService.SaveGalgameAsync(Gal);
        _pvnService.Upload(Gal, PvnUploadProperties.Infos | PvnUploadProperties.ImageLoc);
        _galService.PhrasedEvent -= Update;
    }

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not Galgame galgame)
        {
            return;
        }

        Gal = galgame;
        SelectedRss = Gal.RssType;
        _galService.PhrasedEvent += Update;
        Update();
    }

    partial void OnSelectedRssChanged(RssType value)
    {
        Gal.RssType = value;
        SearchUri = _searchUrlList[(int)value] + Gal.Name.Value;
    }

    [RelayCommand]
    private void OnBack()
    {
        if(_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }

    [RelayCommand]
    private async Task OnGetInfoFromRss(object parameter)
    {
        IsPhrasing = true;
        // 检查是否是 isNameOnly 模式
        if (parameter is string isNameOnly && isNameOnly == "True")
        {
            // 清除目前存储的id信息
            for (var i = 0; i < Galgame.PhraserNumber; i++)
            {
                // 跳过PotatoVn
                if (i == (int)RssType.PotatoVn)
                    continue;
                Gal.Ids[i] = null;
            }
        }
        
        try
        {
            await _galService.PhraseGalInfoAsync(Gal);
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_GetInfoFromRssFailed".GetLocalized(),
                e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
            Update(); // 处理IsPhrasing
        }
    }

    private void Update()
    {
        IsPhrasing = _galService.IsPhrasing;
        LastFetchInfoStr = "GalgameSettingPage_LastFetchInfoTime".GetLocalized(
            new DateTimeToStringConverter().Convert(Gal.LastFetchInfoTime, default!, default!, default!));
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        FileOpenPicker openPicker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".bmp");
        StorageFile? file = await openPicker.PickSingleFileAsync();
        if (file == null) return;
        StorageFile newFile = await file.CopyAsync(await FileHelper.GetFolderAsync(FileHelper.FolderType.Images), 
            $"{Gal.Name.Value}{file.FileType}", NameCollisionOption.ReplaceExisting);
        Gal.ImagePath.Value= newFile.Path;
    }

    [RelayCommand]
    private async Task SetGalgamePathAsync()
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
                // 从库中移除该游戏，再设置路径
                var source = _sourceService.GetGalgameSources().FirstOrDefault(s => s.Galgames.Any(g => g.Galgame == Gal));
                if (source != null)
                {
                    _sourceService.MoveOutNoOperate(source, Gal);
                }

                var folder = file.Path[..file.Path.LastIndexOf('\\')];
                Gal.ExePath = file.Path;
                await _galService.SetLocalPathAsync(Gal, folder);
                

                _infoService.Info(InfoBarSeverity.Success, "GalgameSettingPage_PathSetSuccess".GetLocalized());

            }
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_PathSetFailed".GetLocalized(), e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task SetGalgameExePathAsync()
    {
        if(!Gal.IsLocalGame)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_NotLocalGame".GetLocalized());
            return;
        }
        try
        {
            // 先清空ExePath，再调用函数来设置
            // Gal.ExePath = null;
            await _galService.GetGalgameExeAsync(Gal);
            _infoService.Info(InfoBarSeverity.Success, "GalgameSettingPage_ExePathSetSuccess".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_ExePathSetFailed".GetLocalized(), e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
        }
    }
}
