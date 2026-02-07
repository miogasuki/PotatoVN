using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.WinApp.Base.Contracts.NavigationApi;
using GalgameManager.WinApp.Base.Models.Filters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IPotatoVnApi
{
    //与游戏相关的API
    #region GAMES

    /// <summary>
    /// 获取所有游戏，这个列表只是一个快照（即后续添加的游戏或删除的游戏均不会在这个List中反馈）
    /// </summary>
    /// <returns></returns>
    public List<Galgame> GetAllGames();

    #endregion

    // 与游戏列表界面过滤器相关的 API
    #region FILTERS

    /// <summary>
    /// 游戏列表界面（GameListPage）使用的过滤器。
    /// 这些 API 只影响“游戏列表”的筛选/显示结果，不会修改任何游戏数据。
    /// </summary>
    /// <remarks>
    /// 宿主会在 UI 线程执行这些操作；插件可以从任意线程调用。
    /// </remarks>
    void AddFilter(FilterBase filter);

    /// <summary>
    /// 从游戏列表界面移除一个过滤器（不存在则忽略）。
    /// </summary>
    void DeleteFilter(FilterBase filter);

    /// <summary>
    /// 清空游戏列表界面的过滤器（宿主会保留必要的内置过滤器，例如虚拟游戏开关相关过滤器）。
    /// </summary>
    void ClearFilters();

    /// <summary>
    /// 获取游戏列表界面当前启用的过滤器列表（快照）。
    /// </summary>
    /// <remarks>
    /// 返回的是当前列表的复制品，用于插件侧展示/判断；
    /// 若要修改过滤器，请使用 <see cref="AddFilter"/> / <see cref="DeleteFilter"/> / <see cref="ClearFilters"/>。
    /// </remarks>
    Task<List<FilterBase>> GetFiltersAsync();

    #endregion

    // 与 staff 相关的 API
    #region STAFF

    /// <summary>
    /// 根据 staffId 获取 staff，不存在则返回 null
    /// </summary>
    Staff? GetStaff(Guid? id);

    /// <summary>
    /// 获取所有 staff 列表（快照）
    /// </summary>
    List<Staff> GetStaffs();

    /// <summary>
    /// 获取某个游戏关联的 staff 列表（快照）
    /// </summary>
    List<Staff> GetStaffs(Galgame game);

    /// <summary>
    /// 保存 staff（新增 / 修改）。默认会触发同步逻辑（若宿主启用）。
    /// </summary>
    void SaveStaff(Staff staff, bool sync = true);

    #endregion

    //与插件数据存储相关的API
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

    //与消息相关的API
    #region MESSAGES

    /// <summary>
    /// 全局消息信使
    /// </summary>
    public IMessenger Messenger { get; }

    #endregion

    //与事件/通知相关的API
    #region NOTIFICATIONS

    /// <summary>
    /// 使用InfoBar通知信息，若title与msg均为空则关闭InfoBar
    /// </summary>
    /// <param name="infoBarSeverity"></param>
    /// <param name="title"></param>
    /// <param name="msg"></param>
    /// <param name="displayTimeMs"></param>
    public void Info(InfoBarSeverity infoBarSeverity, string? title = null, string? msg = null,int? displayTimeMs = 3000);

    /// <summary>
    /// 记录并通知事件
    /// </summary>
    /// <param name="infoBarSeverity">严重程度</param>
    /// <param name="title">事件名</param>
    /// <param name="exception">与之相关的异常，若不是异常则不填</param>
    /// <param name="msg">事件信息</param>
    /// <param name="callbackAction">点击按钮后执行的回调</param>
    /// <param name="callbackButtonText">按钮上显示的文字</param>
    public void Event(InfoBarSeverity infoBarSeverity, string title, Exception? exception = null, string? msg = null,
        Action? callbackAction = null, string? callbackButtonText = null);

    /// <summary>
    /// 不严重的非预期错误，仅在开发模式下通知
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="infoBarSeverity"></param>
    /// <param name="e"></param>
    public void DeveloperEvent(InfoBarSeverity infoBarSeverity = InfoBarSeverity.Warning, string? msg = null,
        Exception? e = null);

    /// <summary>
    /// 手动记录日志，默认只将severity >= InfoBarSeverity.Warning的日志通知，开发者模式下通知所有日志 <br/>
    /// <see cref="Event"/>会自动调用该方法记录日志
    /// </summary>
    /// <param name="severity"></param>
    /// <param name="msg"></param>
    public void Log(InfoBarSeverity severity = InfoBarSeverity.Warning, string msg = "");

    #endregion

    //与后台任务相关的API
    #region BG_TASKS

    /// <summary>
    /// 新增后台任务
    /// </summary>
    /// <returns>这个后台任务对应的task</returns>
    public Task AddBgTask(BgTaskBase bgTask);

    /// <summary>
    /// 获取所有后台任务
    /// </summary>
    public IEnumerable<BgTaskBase> GetBgTasks();

    /// <summary>
    /// 获取指定类型的后台任务，如果没有则返回null
    /// </summary>
    /// <param name="key">关键字</param>
    public T? GetBgTask<T>(string key) where T : BgTaskBase;

    #endregion BG_TASKS

    //与软件本体（比如说启动参数）相关的API
    #region HOST

    /// <summary>
    /// 软件的启动参数，正常情况下应该是一个<see cref="AppActivationArguments"/>
    /// </summary>
    object? ActivationArgs { get; }

    /// <summary>
    /// 软件当前使用的语言（切换语言后，这个值会变成目标语言，但需要重启后才生效）
    /// </summary>
    LanguageEnum Language { get; }

    #endregion

    //与界面相关的API
    #region PAGE

    /// <summary>
    /// 软件窗口，注意软件处于最小化到托盘时该值为null
    /// </summary>
    /// <returns></returns>
    Window? GetMainWindow();

    /// <summary>
    /// 跳转到指定页面
    /// </summary>
    /// <param name="page">要跳转到的界面</param>
    /// <param name="parameter">跳转参数，可为空</param>
    void NavigateTo(PageEnum page, object? parameter = null);

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

    /// <summary>
    /// 在主线程执行某个操作（一般用于UI相关操作）
    /// </summary>
    /// <param name="action"></param>
    public void InvokeOnMainThread(Action action);

    #endregion
}
