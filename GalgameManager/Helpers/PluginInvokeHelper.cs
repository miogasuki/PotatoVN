using GalgameManager.Contracts.Services;
using GalgameManager.WinApp.Base.Models;

namespace GalgameManager.Helpers;

public static class PluginInvokeHelper
{
    public static void Invoke (PluginInfo plugin, Action action, IInfoService infoService)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            infoService.PluginEvent(plugin, ex);
        }
    }

    public static T? Invoke<T> (PluginInfo plugin, Func<T> func, IInfoService infoService)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            infoService.PluginEvent(plugin, ex);
        }
        return default;
    }

    public static async Task InvokeAsync (PluginInfo plugin, Func<Task> func, IInfoService infoService)
    {
        try
        {
            await func();
        }
        catch (Exception ex)
        {
            infoService.PluginEvent(plugin, ex);
        }
    }

    public static async Task<T?> InvokeAsync<T> (PluginInfo plugin, Func<Task<T>> func, IInfoService infoService)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            infoService.PluginEvent(plugin, ex);
        }
        return default;
    }
}
