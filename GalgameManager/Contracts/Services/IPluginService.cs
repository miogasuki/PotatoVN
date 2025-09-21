using System.Collections.ObjectModel;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IPluginService
{
    /// <summary>
    /// 新增插件，注意捕获异常
    /// </summary>
    /// <param name="path"></param>
    /// <exception cref="PvnException">已知错误</exception>
    /// <returns></returns>
    public Task AddPluginAsync(string path);
    
    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync();
    
    public Task InitAsync();


    /// <summary>
    /// 加载某个插件，如果已经加载则不操作，注意捕获异常
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="load">是否要加载插件，若设置为false则只把插件加到插件列表里而不加载（初始化插件表时用）</param>
    /// <returns></returns>
    public Task LoadPluginAsync(PluginX plugin, bool load);
}