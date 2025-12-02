using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Loader;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Exceptions;
using GalgameManager.WinApp.Base.Contracts;
using LiteDB;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;


public class PluginComparer : IComparer<PluginX>
{
    public static readonly PluginComparer Instance = new();
    public int Compare(PluginX? x, PluginX? y)
    {
        if (x is null || y is null) return 0;
        // 优先级1: IsDevMode 降序 (true 在前)
        var devCompare = y.IsDevMode.CompareTo(x.IsDevMode); 
        if (devCompare != 0) return devCompare;
        // 优先级2: Name 升序
        return string.Compare(x.Info.Name, y.Info.Name, StringComparison.OrdinalIgnoreCase);
    }
}

public partial class PluginService(
    ILocalSettingsService settingService,
    IBgTaskService bgTaskService,
    IInfoService infoService,
    IMessenger bus) : IPluginService
{
    private ObservableCollection<PluginX> _plugins = [];
    private ILiteCollection<PluginX> _pluginsDb = null!;
    private ILiteCollection<PluginData> _pluginDataDb = null!;

    public bool PluginOffloadInProgress { get; private set; }
    
    private void AddPluginToListSorted(PluginX plugin)
    {
        var i = 0;
        while (i < _plugins.Count && PluginComparer.Instance.Compare(_plugins[i], plugin) < 0)
        {
            i++;
        }
        _plugins.Insert(i, plugin);
    }
    
    private void RemovePluginFromList(PluginX plugin)
    {
        _plugins.Remove(plugin);
    }
    
    public void PluginSetData(PluginX plugin, string? data)
    {
        PluginData newData = new() {
            PluginId = plugin.Id,
            Data = data,
        };
        _pluginDataDb.Upsert(newData);
    }
    
    public void PluginDeleteData(PluginX plugin) => _pluginDataDb.Delete(plugin.Info.Id);

    public async Task AddPluginAsync(string path, bool isDev)
    {
        // 如果删除过正常插件，即使是 Dev 模式也会禁止加载新的插件。
        if (PluginOffloadInProgress) 
            throw new PvnException("PluginService_PluginOffloadInProgress".GetLocalized());
        if (_plugins.Any(p => Utils.ArePathsEqual(path, p.Path)))   
            throw new PvnException($"plugin in {path} already initialized");
        (IPlugin plugin, PluginLoadContext contex) tmp = await LoadPluginInternalAsync(path, isDev);
        PluginX plugin = new(tmp.plugin, path, tmp.contex)
        {
            Enable = true,
            IsDevMode = isDev,
        };
        await LoadPluginAsync(plugin, true);
        // 使用 Insert 来做最后的判重，防止重复加载 Dev 插件。
        _pluginsDb.Insert(plugin);
    }

    public async Task DeletePluginAsync(PluginX plugin, bool deleteData)
    {
        plugin.ToDelete = true;
        plugin.ToDeleteData = deleteData;
        if (!plugin.IsDevMode)
        {
            // 对于 Dev Plugin，我们立即删除对应的 Plugin，同时不进入 Offload State
            PluginOffloadInProgress = true;
        }
        if (plugin.IsDevMode)
        {
            try
            {
                // Only wait for 5 seconds for unload to complete.
                TimeSpan initialTimeout = TimeSpan.FromSeconds(5);
                TimeSpan maxTimeout = TimeSpan.FromSeconds(60);

                using var cts = new CancellationTokenSource();

                var startTime = DateTime.UtcNow;
                var deadline = startTime.Add(initialTimeout);
                var hardDeadline = startTime.Add(maxTimeout);
                cts.CancelAfter(hardDeadline - startTime);

                Action<TimeSpan> extendWaitHandler = (extraTime) =>
                {
                    DateTime newDeadline = DateTime.UtcNow.Add(extraTime);
                    if (newDeadline > hardDeadline) newDeadline = hardDeadline;
                    if (newDeadline > deadline) deadline = newDeadline;
                };

                if (plugin.Plugin is not null)
                {
                    Task uninstallTask = plugin.Plugin.OnUninstallAsync(deleteData, extendWaitHandler, cts.Token);
                    while (!uninstallTask.IsCompleted)
                    {
                        var now = DateTime.UtcNow;
                        if (now >= deadline)
                        {
                            await cts.CancelAsync();
                            throw new TimeoutException("Plugin Uninstall Timeout");
                        }

                        var waitTime = deadline - now;
                        var delayTask = Task.Delay(waitTime, cts.Token);
                        var completeTask = await Task.WhenAny(uninstallTask, delayTask);

                        if (completeTask == uninstallTask)
                            break;
                    }
                    await uninstallTask;
                }
            }
            catch (OperationCanceledException)
            {
                /* 插件内部中止操作 */
                App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Warning,
                    $"Abort",
                    msg: $"Dev plugin {plugin.Info.Name} uninstalled internal abort"
                );
            }
            catch (TimeoutException)
            {
                /* 卸载超时：硬超时 */
                App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Warning,
                    $"Timeout",
                    msg: $"Dev plugin {plugin.Info.Name} uninstalled timeout"
                );
            }
            catch (Exception e)
            {
                 /* 卸载错误 */
                 App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Warning,
                 $"Dev plugin {plugin.Info.Name} uninstalled with errors",
                 msg: $"Dev plugin {plugin.Info.Name} uninstalled with errors...\n${e}"
                 );
            }
            _pluginsDb.Delete(plugin.Info.Id);
            if (deleteData)
                PluginDeleteData(plugin);
            plugin.ForceUnload();
        }
        else
        {
            SavePlugin(plugin);
        }
        await UiThreadInvokeHelper.InvokeAsync(() => RemovePluginFromList(plugin));
    }

    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync() => Task.FromResult(_plugins);

    public async Task InitAsync()
    {
        await Task.CompletedTask; //预留异步
        _pluginsDb = settingService.Database.GetCollection<PluginX>("plugin");
        _pluginDataDb = settingService.Database.GetCollection<PluginData>("plugin_data");
        PluginDir = new DirectoryInfo((await FileHelper.GetFolderAsync(FileHelper.FolderType.Plugins)).Path);
        if (!PluginDir.Exists) PluginDir.Create();
        _ = bgTaskService.AddBgTask(new LoadPluginTask());
    }

    public async Task LoadPluginAsync(PluginX plugin, bool load)
    {
        if (plugin.IsLoaded) return;
        if (load)
        {
            if (plugin.Plugin is null)
            {
                (IPlugin plugin, PluginLoadContext contex) tmp = await LoadPluginInternalAsync(plugin.Path, plugin.IsDevMode);
                plugin.Plugin = tmp.plugin;
                plugin.LoadContext = tmp.contex;
            }
            plugin.Info = plugin.Plugin.Info;
            await plugin.Plugin.InitializeAsync(new PotatoVnApiHost(plugin));
            plugin.IsLoaded = true;
        }
        if (_plugins.All(p => p.Info.Id != plugin.Info.Id))
            await UiThreadInvokeHelper.InvokeAsync(() => AddPluginToListSorted(plugin));
        plugin.PropertyChanged -= OnPluginOnPropertyChanged;
        plugin.PropertyChanged += OnPluginOnPropertyChanged;
        if (load) bus.Send(new PluginLoadArgs { Plugin = plugin.Plugin! });
        return;

        async void OnPluginOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            // FIXME(kuriko): 这里我们没有考虑 name 和 devMode 属性变化导致的插件列表重排序问题
            try
            {
                if (args.PropertyName != nameof(PluginX.Enable)) return;
                if (plugin.Enable)
                    await LoadPluginAsync(plugin, true);
                else
                {
                    infoService.Info(InfoBarSeverity.Success, msg: "PluginService_PluginUnloaded".GetLocalized());
                    PluginOffloadInProgress = true;
                }
                _pluginsDb.Update(plugin);
            }
            catch (Exception e)
            {
                infoService.Event(EventType.AppError, InfoBarSeverity.Warning,
                    "PluginService_PluginStatusChangedFailed".GetLocalized(), exception: e);
            }
        }
    }

    public DirectoryInfo PluginDir { get; private set; } = null!;

    public void ThrowPluginExceptionEvent(PluginX plugin, Exception e, string msgHeader)
    {
        infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
            "PluginService_PluginError".GetLocalized(plugin.Info.Name),
            msg: msgHeader + "PluginService_PluginError_Msg".GetLocalized(e.ToString()));
    }

    private static async Task<(IPlugin plugin, PluginLoadContext contex)> LoadPluginInternalAsync(string path, bool isDev)
    {
        await Task.CompletedTask; //预留异步
        if (!Directory.Exists(path)) throw new PvnPathNotExist(path);
        
        // 查找与目录同名的 DLL
        var directoryName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var pluginFile = Path.Combine(path, $"{directoryName}.dll");
        if (!File.Exists(pluginFile))
            pluginFile = Directory.GetFiles(path, "PotatoVN.App.PluginBase.dll").FirstOrDefault();
        if (pluginFile == null || !File.Exists(pluginFile)) 
            throw new PvnException($"plugin dll of {path} not found");
        PluginLoadContext loadContext = new(pluginFile);

        Assembly pluginAssembly;
        if (isDev)
        {
            var pdbPath = Path.ChangeExtension(pluginFile, ".pdb");
            FileStream? pdbStream = File.Exists(pdbPath) 
                ? new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read) 
                : null;
            await using (pdbStream)
            {
                await using FileStream fs = new (pluginFile, FileMode.Open, FileAccess.Read);
                pluginAssembly = loadContext.LoadFromStream(fs, pdbStream);
            }
        }
        else
        {
            pluginAssembly = loadContext.LoadFromAssemblyPath(pluginFile);
        }
        
        Type? pluginType = pluginAssembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface);
        if (pluginType == null) throw new PvnException($"no valid plugin found in {path}");
        return ((IPlugin)Activator.CreateInstance(pluginType)!, loadContext);
    }

    private void SavePlugin(PluginX plugin) => _pluginsDb.Update(plugin);

    public class PluginData
    {
        [BsonId] public Guid PluginId { get; set; }
        public string? Data { get; set; }
    }
}

public class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);
    private readonly HashSet<string> _sharedAssembliesBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        // WinAppSDK & WinUI
        "Microsoft.WinUI",
        "Microsoft.WindowsAppRuntime.Bootstrap.Net",
        "Microsoft.Windows.SDK.NET",
        "WinRT.Runtime",
        "Microsoft.Graphics.Imaging.Projection",
        "Microsoft.Web.WebView2.Core",
        "Microsoft.Windows.AppLifecycle.Projection",
        "Microsoft.Windows.AppNotifications.Projection",
        "Microsoft.Windows.System.Projection",
        // CommunityToolkit
        "CommunityToolkit.Common",
        "CommunityToolkit.Mvvm",
        "CommunityToolkit.WinUI.Collections",
        "CommunityToolkit.WinUI.Extensions",
        "CommunityToolkit.WinUI.Helpers",
    };

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null && !_sharedAssembliesBlacklist.Contains(assemblyName.Name!))
            return LoadFromAssemblyPath(assemblyPath);
        // dll不存在/黑名单（WinAppSDK）dll
        return null;
    }
}