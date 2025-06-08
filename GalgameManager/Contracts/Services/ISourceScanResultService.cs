using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface ISourceScanResultService
{
    Task SaveScanResultAsync(GalgameScanResult scanResult);
    Task<GalgameScanResult?> GetScanResultAsync(Guid id);
    Task<List<GalgameScanResult>> GetAllScanResultsAsync();
    Task DeleteScanResultAsync(Guid id);
}