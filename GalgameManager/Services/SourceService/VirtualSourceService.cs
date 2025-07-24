using GalgameManager.Contracts.Services;
using GalgameManager.Models;

namespace GalgameManager.Services;

public class VirtualSourceService : ISourceScanResultService
{
    public void SaveScanResult(GalgameScanResult scanResult)
    {
    }

    public GalgameScanResult? GetScanResult(Guid id) => null;

    public List<GalgameScanResult> GetAllScanResults() => [];

    public void DeleteScanResult(Guid id)
    {
    }
}