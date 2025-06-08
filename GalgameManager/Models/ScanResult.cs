using LiteDB;

namespace GalgameManager.Models;

public enum ScanResultType
{
    Information, // For general messages, like scan start time or non-error specific logs
    Success,
    AlreadyExists,
    Failed
}

public class PathScanResultItem
{
    public string Path { get; set; } = string.Empty;
    public ScanResultType ResultType { get; set; }
    public string Message { get; set; } = string.Empty; // e.g., game name for AlreadyExists, error for Failed, specific info
}

public class GalgameScanResult
{
    [BsonId]
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }
    public List<PathScanResultItem> Results { get; set; } = new();
}
