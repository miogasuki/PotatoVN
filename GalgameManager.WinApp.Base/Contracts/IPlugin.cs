using System.Threading.Tasks;
using GalgameManager.WinApp.Base.Models;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IPlugin
{
    /// <summary>
    /// 返回本插件的信息（ID、插件名、支持版本等） <p/>
    /// 这个属性必须确保任何时候都可用（比如说插件没被加载的时候）
    /// </summary>
    public PluginInfo Info { get; }
    
    /// <summary>
    /// 插件加载时会被调用
    /// </summary>
    /// <returns></returns>
    public Task InitializeAsync(IPotatoVnApi hostApi);
}