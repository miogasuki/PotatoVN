using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Models;
using LiteDB;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models;

public partial class PluginX : ObservableObject
{
    [BsonId] public Guid Id { get; set; }
    [BsonIgnore] public IPlugin? Plugin { get; set; }
    public PluginInfo Info { get; set; } 
    [ObservableProperty] private string _path;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Logo))] private string? _logoUrl;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Logo))] private string? _logoPath;
    public string? Logo => !string.IsNullOrEmpty(LogoPath) ? LogoPath : LogoUrl;
    [ObservableProperty] private DateTime _releaseDate;

    /// 是否启用，注意，设置为false只会让下次启动不加载该插件，不会在当前卸载
    [ObservableProperty] private bool _enable = false;
    /// 如果设置为true，该插件会在下次启动时被删除
    public bool ToDelete { get; set; }
    /// 删除插件时是否连同数据一起删除
    public bool ToDeleteData { get; set; }
    /// 是否已经加载
    [BsonIgnore] public bool IsLoaded { get; set; } = false;
    [BsonIgnore] public PluginLoadContext LoadContext { get; set; }
    
    // 插件是否是在 Dev 模式下加载的
    [ObservableProperty] private bool _isDevMode = false;
    
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

    /// <summary>
    /// 对获取插件UI的函数的包装，对于执行时间过长的函数（这会卡住主进程）进行提示
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    public UIElement? GetPluginUi(Func<UIElement?> func)
    {
        DateTime start = DateTime.Now;
        UIElement? ui = func();
        DateTime end = DateTime.Now;
        TimeSpan span = end - start;
        if (span.TotalMilliseconds > 1000)
            App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Informational,
                "PluginX_UiSlow_Title".GetLocalized(), msg: "PluginX_UiSlow_Msg".GetLocalized(Info.Name));
        return ui;
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