using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;
using GalgameManager.Models.Sources;

namespace GalgameManager.ViewModels;

public partial class ScanResultViewModel(
    ISourceScanResultService sourceScanResultService,
    IGalgameSourceCollectionService galgameSourceCollectionService,
    IInfoService infoService)
    : ObservableRecipient, INavigationAware
{
    private GalgameScanResult? _scanResult;
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _sourceName = string.Empty;
    [ObservableProperty]
    private string _sourcePath = string.Empty; // Might be tricky to get accurately if source was deleted
    [ObservableProperty]
    private DateTime _scanTime;
    [ObservableProperty]
    private ObservableCollection<PathScanResultItem> _displayResults = new();

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            if (parameter is Guid scanResultId)
            {
                _scanResult = await sourceScanResultService.GetScanResultAsync(scanResultId);
                if (_scanResult != null)
                {
                    SourceName = _scanResult.SourceName;
                    ScanTime = _scanResult.ScanTime;
                    GalgameSourceBase? source = galgameSourceCollectionService.GetGalgameSourceFromId(_scanResult.SourceId);
                    SourcePath = source?.Path ?? "N/A (Source may have been removed or changed)";
                    ApplyFilter();
                }
                else
                {
                    // Handle case where scan result is not found
                    SourceName = "Error: Scan result not found.";
                }
            }
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
    }

    public void OnNavigatedFrom()
    {
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            SetProperty(ref _filterText, value);
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        if (_scanResult == null) return;

        IEnumerable<PathScanResultItem> filtered;
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = _scanResult.Results;
        }
        else
        {
            var filter = FilterText.ToLowerInvariant();
            filtered = _scanResult.Results.Where(r => r.Path.ToLowerInvariant().Contains(filter) || 
                                                      (r.Message.ToLowerInvariant().Contains(filter)));
        }
        DisplayResults = new ObservableCollection<PathScanResultItem>(filtered);
    }
}
