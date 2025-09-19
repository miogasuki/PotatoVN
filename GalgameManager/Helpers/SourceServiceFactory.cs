using GalgameManager.Contracts.Services;
using GalgameManager.Models.Sources;
using GalgameManager.Services;

namespace GalgameManager.Helpers;

public static class SourceServiceFactory
{
    private static readonly Dictionary<GalgameSourceType, IGalgameSourceService> SourceServices = new();
    private static readonly object Lock = new();

    public static IGalgameSourceService GetSourceService(GalgameSourceType type)
    {
        lock (Lock)
        {
            if (SourceServices.TryGetValue(type, out IGalgameSourceService? value)) return value;
            value = type switch
            {
                GalgameSourceType.LocalFolder => App.GetService<LocalFolderSourceService>(),
                GalgameSourceType.UnKnown => throw new ArgumentException("UnKnow source"),
                GalgameSourceType.LocalZip => throw new NotImplementedException(),
                GalgameSourceType.Virtual => App.GetService<VirtualSourceService>(),
                GalgameSourceType.Steam => App.GetService<SteamSourceService>(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            SourceServices[type] = value;
            return value;
        }
    }
}