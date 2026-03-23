using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;

namespace GalgameManager.Services;

internal static class PluginXamlHost
{
    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<string, PluginAssemblyRegistration> Registrations = new();

    private static Application? _application;
    private static PropertyInfo? _metadataProviderProperty;
    private static PropertyInfo? _typeInfoProviderProperty;
    private static PropertyInfo? _otherProvidersProperty;

    public static void Initialize(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        lock (SyncRoot)
        {
            if (_application is not null)
            {
                if (ReferenceEquals(_application, application))
                    return;
                throw new InvalidOperationException("PluginXamlHost has already been initialized.");
            }

            _application = application;
            Type appType = application.GetType();
            _metadataProviderProperty = appType.GetProperty("_AppProvider",
                                            BindingFlags.NonPublic | BindingFlags.Instance)
                                        ?? throw new InvalidOperationException(
                                            $"Failed to access XAML metadata provider on {appType.FullName}.");
            _typeInfoProviderProperty = _metadataProviderProperty.PropertyType.GetProperty("Provider",
                                            BindingFlags.NonPublic | BindingFlags.Instance)
                                        ?? throw new InvalidOperationException(
                                            $"Failed to access XAML type provider on {appType.FullName}.");
            _otherProvidersProperty = _typeInfoProviderProperty.PropertyType.GetProperty("OtherProviders",
                                            BindingFlags.NonPublic | BindingFlags.Instance)
                                      ?? throw new InvalidOperationException(
                                          $"Failed to access XAML metadata provider list on {appType.FullName}.");
        }
    }

    public static async Task<IDisposable> RegisterPluginAssemblyAsync(Assembly assembly, string pluginRootPath)
    {
        if (_application is null) throw new InvalidOperationException("PluginXamlHost is not initialized."); //不应该发生
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginRootPath);

        var assemblyFullName = assembly.FullName
                               ?? throw new InvalidOperationException("Plugin assembly full name is missing.");
        var fullPluginRootPath = Path.GetFullPath(pluginRootPath);

        if (Registrations.TryGetValue(assemblyFullName, out PluginAssemblyRegistration? existingRegistration))
            existingRegistration.Dispose();

        PluginAssemblyRegistration registration = new(assembly, fullPluginRootPath);
        await registration.InitializeAsync();
        Registrations[assemblyFullName] = registration;
        return registration;
    }

    private static IDisposable RegisterXamlMetadataProvider(IXamlMetadataProvider provider)
    {
        lock (SyncRoot)
        {
            GetOtherProviders().Add(provider);
        }

        return new MetadataProviderRegistration(provider);
    }

    private static void Unregister(PluginAssemblyRegistration registration)
    {
        if (Registrations.TryGetValue(registration.AssemblyFullName, out PluginAssemblyRegistration? current)
            && ReferenceEquals(current, registration))
            Registrations.TryRemove(registration.AssemblyFullName, out _);
    }

    private static List<IXamlMetadataProvider> GetOtherProviders()
    {
        Application application = _application ?? throw new InvalidOperationException("PluginXamlHost is not initialized.");
        var appProvider = _metadataProviderProperty?.GetValue(application)
                          ?? throw new InvalidOperationException("Failed to access WinUI app metadata provider.");
        var provider = _typeInfoProviderProperty?.GetValue(appProvider)
                       ?? throw new InvalidOperationException("Failed to access WinUI XAML type provider.");
        return (_otherProvidersProperty?.GetValue(provider) as List<IXamlMetadataProvider>)
               ?? throw new InvalidOperationException("Failed to access WinUI XAML metadata provider list.");
    }

    private sealed class MetadataProviderRegistration(IXamlMetadataProvider provider) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (SyncRoot)
            {
                GetOtherProviders().Remove(provider);
            }

            _disposed = true;
        }
    }

    private sealed class PluginAssemblyRegistration(Assembly assembly, string pluginRootPath) : IDisposable
    {
        private readonly List<IDisposable> _providerRegistrations = [];
        private bool _disposed;

        public string AssemblyFullName { get; } = assembly.FullName ?? throw new InvalidOperationException(
                                                      "Plugin assembly full name is missing.");
        public async Task InitializeAsync()
        {
            await LoadPriFilesAsync();
            RegisterMetadataProviders();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (IDisposable registration in _providerRegistrations)
                registration.Dispose();
            _providerRegistrations.Clear();
            Unregister(this);
            _disposed = true;
        }

        private async Task LoadPriFilesAsync()
        {
            var assemblySimpleName = assembly.GetName().Name
                                     ?? throw new InvalidOperationException(
                                         "Plugin assembly name is missing.");
            HashSet<string> candidates =
            [
                Path.Combine(pluginRootPath, "resources.pri"),
                Path.Combine(pluginRootPath, $"{assemblySimpleName}.pri"),
            ];

            List<StorageFile> files = [];
            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                files.Add(await StorageFile.GetFileFromPathAsync(candidate));
            }

            if (files.Count > 0) ResourceManager.Current.LoadPriFiles(files);
        }

        private void RegisterMetadataProviders()
        {
            foreach (Type type in GetLoadableTypes())
            {
                if (!typeof(IXamlMetadataProvider).IsAssignableFrom(type))
                    continue;
                if (type is { IsInterface: true } or { IsAbstract: true })
                    continue;
                if (Activator.CreateInstance(type) is IXamlMetadataProvider provider)
                    _providerRegistrations.Add(RegisterXamlMetadataProvider(provider));
            }
        }

        private IEnumerable<Type> GetLoadableTypes()
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null).Cast<Type>();
            }
        }
    }
}
