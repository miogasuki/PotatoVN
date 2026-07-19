using System;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;

namespace GalgameManager.WinApp.Base.Helpers;

public static class UiThreadInvokeHelper
{
    private static DispatcherQueue _dispatcherQueue = null!;
    
    [Obsolete("这个方法只会被App初始化时调用，请不要在其他地方调用")]
    public static void Init(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }
    
    internal static void Invoke(Func<Task> action)
    {
        _dispatcherQueue.EnqueueAsync(async () =>
        {
            await action();
        });
    }
    
    internal static void Invoke(Action action)
    {
        _dispatcherQueue.EnqueueAsync(action);
    }
}