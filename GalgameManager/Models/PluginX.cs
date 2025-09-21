using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Services;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Models;
using LiteDB;

namespace GalgameManager.Models;

public partial class PluginX : ObservableObject
{
    [BsonId] public Guid Id { get; set; }
    [BsonIgnore] public IPlugin Plugin { get; set; }
    public PluginInfo Info { get; set; } 
    [ObservableProperty] private string _path;
    /// 是否启用，注意，设置为false只会让下次启动不加载该插件，不会在当前卸载
    [ObservableProperty] private bool _enable = false;
    /// 是否已经加载
    [BsonIgnore] public bool IsLoaded { get; set; } = false;
    [BsonIgnore] public PluginLoadContext LoadContext { get; set; }
    /// 插件自己存储的数据
    public string? Data { get; set; }

    [Obsolete("For deserialization only", true)]
    public PluginX()
    {
        Plugin = null!;
        Info = null!;
        _path = string.Empty;
        LoadContext = null!;
    }

    /// <inheritdoc/>
    public PluginX(IPlugin plugin, string path, PluginLoadContext context)
    {
        Plugin = plugin;
        _path = path;
        LoadContext = context;
        Info = plugin.Info.ShallowClone();
        Id = Info.Id;
    }
}

public static class PluginExtensions
{
    public static PluginInfo ShallowClone(this PluginInfo info) => new()
    {
        Description = info.Description,
        Id = info.Id,
        Name = info.Name
    };
}