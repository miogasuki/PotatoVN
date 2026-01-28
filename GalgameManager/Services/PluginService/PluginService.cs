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

    public void PluginSetData(PluginX plugin, string? data)
    {
        PluginData newData = new()
        {
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

    public async Task InvokePluginOnUninstall(PluginX plugin)
    {
        try
        {
            await plugin.ExecuteUninstallWithTimeoutAsync();
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
            await InvokePluginOnUninstall(plugin);
        }

        // 先移除内存中的 Plugin，触发 UI 更新，确保 UI 释放对 Plugin 程序集中类型的引用
        await UiThreadInvokeHelper.InvokeAsync(() => _plugins.Remove(plugin));

        // 非 UI 操作到后台线程，避免阻塞 UI，同时给予 UI 线程处理可视树更新的时间
        await Task.Run(() =>
        {
            if (plugin.IsDevMode)
            {
                _pluginsDb.Delete(plugin.Info.Id);
                if (deleteData)
                    PluginDeleteData(plugin);
                plugin.ForceUnload();
            }
            else
            {
                SavePlugin(plugin);
            }
        });
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
            await UiThreadInvokeHelper.InvokeAsync(() => _plugins.Add(plugin));
        plugin.PropertyChanged -= OnPluginOnPropertyChanged;
        plugin.PropertyChanged += OnPluginOnPropertyChanged;
        if (load) bus.Send(new PluginLoadArgs { Plugin = plugin.Plugin! });
        return;

        async void OnPluginOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
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
        PluginLoadContext loadContext = new(pluginFile, isDev);

        Assembly pluginAssembly;
        if (isDev)
        {
            var pdbPath = Path.ChangeExtension(pluginFile, ".pdb");
            FileStream? pdbStream = File.Exists(pdbPath)
                ? new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                : null;
            await using (pdbStream)
            {
                await using FileStream fs = new(pluginFile, FileMode.Open, FileAccess.Read);
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

public class PluginLoadContext(string pluginPath, bool isDev = false) : AssemblyLoadContext(isCollectible: true)
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
        {
            if (isDev)
            {
                using var fs = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
                if (File.Exists(pdbPath))
                {
                    using var pdbFs = new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return LoadFromStream(fs, pdbFs);
                }
                return LoadFromStream(fs);
            }
            return LoadFromAssemblyPath(assemblyPath);
        }
        // dll不存在/黑名单（WinAppSDK）dll
        return null;
    }
}
