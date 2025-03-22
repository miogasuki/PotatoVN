using System.Runtime.InteropServices;
using CommunityToolkit.WinUI;

namespace GalgameManager.Helpers;

public static class UiThreadInvokeHelper
{
    public static async Task InvokeAsync(Action? action)
    {
        if(action is null) return;
        await App.DispatcherQueue.EnqueueAsync(action);
    }

    public static async Task InvokeAsync(Func<Task> action)
    {
        await App.DispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    public static void Invoke(Func<Task> action)
    {
        App.DispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    public static void Invoke(Action action)
    {
        App.DispatcherQueue.EnqueueAsync(action);
    }

    public static void IgnoreComException(Action action)
    {
        try
        {
            action();
        }
        catch (COMException)
        {
            // ignored
        }
    }
    
    public static void IgnoreComException<T1>(Action<T1> action, T1 arg1)
    {
        try
        {
            action(arg1);
        }
        catch (COMException)
        {
            // ignored
        }
    }
}