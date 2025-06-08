using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using LiteDB;

namespace GalgameManager.Services;

public class SourceScanResultService(ILocalSettingsService localSettings) : ISourceScanResultService
{
    private ILiteCollection<GalgameScanResult>? _scanResultDbSet;

    private const string CollectionName = "scan_results";

    private async Task EnsureDbSetInitialized()
    {
        if (_scanResultDbSet is not null) return;
        await Task.Run(() => // Ensure LiteDB operations are on a background thread if needed
        {
            _scanResultDbSet = localSettings.Database.GetCollection<GalgameScanResult>(CollectionName);
        });
    }

    public async Task SaveScanResultAsync(GalgameScanResult scanResult)
    {
        await EnsureDbSetInitialized();
        await Task.Run(() =>
        {
            _scanResultDbSet!.Delete(scanResult.SourceId); //覆盖旧记录
            _scanResultDbSet!.Upsert(scanResult);
        });
    }

    public async Task<GalgameScanResult?> GetScanResultAsync(Guid id)
    {
        await EnsureDbSetInitialized();
        return await Task.Run(() => _scanResultDbSet!.FindById(id));
    }

    public async Task<List<GalgameScanResult>> GetAllScanResultsAsync()
    {
        await EnsureDbSetInitialized();
        return await Task.Run(() => _scanResultDbSet!.FindAll().ToList());
    }

    public async Task DeleteScanResultAsync(Guid id)
    {
        await EnsureDbSetInitialized();
        await Task.Run(() => _scanResultDbSet!.Delete(id));
    }
}