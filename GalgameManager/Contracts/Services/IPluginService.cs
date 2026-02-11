using System.Collections.ObjectModel;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IPluginService
{
    /// <summary>
    /// 新增插件，注意捕获异常
    /// </summary>
    /// <param name="path"></param>
    /// <param name="isDev"> 是否以 Dev 模式加载插件 </param>
    /// <exception cref="PvnException">已知错误</exception>
    /// <returns></returns>
    public Task AddPluginAsync(string path, bool isDev);

    /// <summary>
    /// 标记一个插件要卸载，下次启动时会卸载插件。 <br/>
    /// 对于开发者的插件不会删除文件，只会取消加载并从插件列表移除
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="deleteData">是否删除插件数据</param>
    /// <returns></returns>
    public Task DeletePluginAsync(PluginX plugin, bool deleteData);

    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync();

    public Task InitAsync();

    /// <summary>
    /// 加载某个插件，如果已经加载则不操作，注意捕获异常
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="load">是否要加载插件，若设置为false则只把插件加到插件列表里而不加载（初始化插件表时用）</param>
    /// <returns></returns>
    public Task LoadPluginAsync(PluginX plugin, bool load);

    /// <summary>
    /// 立即删除插件的关联数据
    /// </summary>
    /// <param name="plugin"></param>
    public void PluginDeleteData(PluginX plugin);

    /// <summary>
    /// 插件存放目录
    /// </summary>
    public DirectoryInfo PluginDir { get; }

    /// <summary>
    /// 是否有正在等待取消加载/删除的插件
    /// </summary>
    public bool PluginOffloadInProgress { get; }

    /// <summary>
    /// 立即设置插件的关联数据
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="data"></param>
    public void PluginSetData(PluginX plugin, string? data);

    /// <summary>
    /// 保存插件信息
    /// </summary>
    /// <param name="plugin"></param>
    public void SavePlugin(PluginX plugin);

    /// <summary>
    /// 当调用插件功能抛出异常时可以调用此方法提醒用户（内部会调用infoService发送一个Event）
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="e"></param>
    /// <param name="msgHeader">消息头，最后消息会这样组合：mesHeader+"建议联系插件开发者了解解决方案~\n\n以下为详细报错：\n"</param>
    public void ThrowPluginExceptionEvent(PluginX plugin, Exception e, string msgHeader);
}
