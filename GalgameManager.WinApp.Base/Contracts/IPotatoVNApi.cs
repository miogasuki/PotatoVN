using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IPotatoVnApi
{
    #region DATA

    /// <summary>
    /// 读取本插件存储的数据
    /// </summary>
    /// <returns></returns>
    public Task<string?> GetDataAsync();
    
    /// <summary>
    /// 保存本插件存储的数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public Task SaveDataAsync(string data);

    #endregion
    
    #region UTILS

    /// <summary>
    /// 下载图片并保存到本地
    /// </summary>
    /// <param name="imageUrl">图片链接</param>
    /// <param name="imageName">图片名，<b>不用后缀名</b></param>
    /// <param name="client">自定义httpClient，若不指定则使用potatovn的默认HttpClient</param>
    /// <param name="onException">异常回调</param>
    /// <returns>下载后图片路径，若下载失败返回null</returns>
    public Task<string?> DownloadImageAsync(string imageUrl, string imageName, HttpClient? client,
        Action<Exception>? onException = null);

    /// <summary>
    /// 获取插件所在路径
    /// </summary>
    /// <returns></returns>
    public string GetPluginPath();
    
    #endregion
}