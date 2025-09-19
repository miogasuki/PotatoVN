using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.Models;

/// <summary>
/// 当插件被加载时触发
/// </summary>
public class PluginLoadArgs
{
    public required IPlugin Plugin { get; init; }
}

/// <summary>
/// 当插件被卸载前触发
/// </summary>
public class PluginOffloadArgs
{
    public required PluginX Plugin { get; init; }
}