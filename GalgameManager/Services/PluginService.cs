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

public class PluginService(
    ILocalSettingsService settingService,
    IBgTaskService bgTaskService,
    IInfoService infoService,
    IMessenger bus) : IPluginService
{
    private ObservableCollection<PluginX> _plugins = [];
    private ILiteCollection<PluginX> _pluginsDb = null!;
    
    public async Task AddPluginAsync(string path)
    {
        if (_plugins.Any(p => Utils.ArePathsEqual(path, p.Path)))   
            throw new PvnException($"plugin in {path} already initialized");
        (IPlugin plugin, PluginLoadContext contex) tmp = await LoadPluginInternalAsync(path);
        PluginX plugin = new(tmp.plugin, path, tmp.contex)
        {
            Enable = true,
        };
        await LoadPluginAsync(plugin, true);
        _pluginsDb.Insert(plugin);
    }

    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync() => Task.FromResult(_plugins);

    public async Task InitAsync()
    {
        await Task.CompletedTask; //预留异步
        _pluginsDb = settingService.Database.GetCollection<PluginX>("plugin");
        _ = bgTaskService.AddBgTask(new LoadPluginTask());
    }

    public async Task LoadPluginAsync(PluginX plugin, bool load)
    {
        if (plugin.IsLoaded) return;
        if (load)
        {
            (IPlugin plugin, PluginLoadContext contex) tmp = await LoadPluginInternalAsync(plugin.Path);
            plugin.Plugin = tmp.plugin;
            plugin.LoadContext = tmp.contex;
            plugin.IsLoaded = true;
        }
        if (_plugins.All(p => p.Info.Id != plugin.Info.Id))
            await UiThreadInvokeHelper.InvokeAsync(() => { _plugins.Add(plugin); });
        plugin.PropertyChanged -= OnPluginOnPropertyChanged;
        plugin.PropertyChanged += OnPluginOnPropertyChanged;
        if (load) bus.Send(new PluginLoadArgs { Plugin = plugin.Plugin });
        return;

        async void OnPluginOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            try
            {
                if (args.PropertyName != nameof(PluginX.Enable)) return;
                if (plugin.Enable)
                    await LoadPluginAsync(plugin, true);
                else
                    infoService.Info(InfoBarSeverity.Success, msg: "PluginService_PluginUnloaded".GetLocalized());
                _pluginsDb.Update(plugin);
            }
            catch (Exception e)
            {
                infoService.Event(EventType.AppError, InfoBarSeverity.Warning,
                    "PluginService_PluginStatusChangedFailed".GetLocalized(), exception: e);
            }
        }
    }
    
    private static async Task<(IPlugin plugin, PluginLoadContext contex)> LoadPluginInternalAsync(string path)
    {
        await Task.CompletedTask; //预留异步
        if (!Directory.Exists(path)) throw new PvnPathNotExist(path);
        var pluginFile = Directory.GetFiles(path, "PotatoVN.App.PluginBase.dll").FirstOrDefault();
        if (pluginFile == null) throw new PvnException($"plugin dll of {path} not found");
        PluginLoadContext loadContext = new(pluginFile);
        Assembly pluginAssembly = loadContext.LoadFromAssemblyPath(pluginFile);
        Type? pluginType = pluginAssembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface);
        if (pluginType == null) throw new PvnException($"no valid plugin found in {path}");
        return ((IPlugin)Activator.CreateInstance(pluginType)!, loadContext);
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