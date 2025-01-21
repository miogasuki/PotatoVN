using System.Collections.ObjectModel;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.Sources;

namespace GalgameManager.Contracts.Services;

public interface IGalgameCollectionService
{
    public Task InitAsync();

    public Task StartAsync();

    /// <summary>
    /// 当某款游戏被修改（被添加/设置本地路径）时触发
    /// </summary>
    public event Action<Galgame>? GalgameChangedEvent;

    /// <summary>
    /// 添加一个游戏，注意捕获异常
    /// </summary>
    /// <param name="sourceType">游戏所属库</param>
    /// <param name="path">游戏文件夹路径</param>
    /// <param name="force">没有在信息源中搜到该游戏时是否强制添加游戏</param>
    /// <param name="requireConfirm">是否要确认搜刮结果</param>
    /// <returns></returns>
    public Task<Galgame> AddGameAsync(GalgameSourceType sourceType, string path, bool force,
        bool requireConfirm = true);
    
    /// <summary>
    /// 指定某个游戏的本地路径，注意捕获异常
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <param name="path">游戏文件夹路径</param>
    /// <returns></returns>
    public Task<Galgame> SetLocalPathAsync(Galgame galgame, string path);

    /// <summary>
    /// 移除一个galgame
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <param name="removeFromDisk">是否要从硬盘移除游戏</param>
    public Task RemoveGalgame(Galgame galgame, bool removeFromDisk = false);

    /// <summary>
    /// 获取所有galgame
    /// </summary>
    public ObservableCollection<Galgame> Galgames { get; }
    
    /// <summary>
    /// 获取UID获取游戏，若没有则返回null<br/>
    /// Uid比较规则见：<see cref="GalgameUid.IsSame"/>
    /// </summary>
    public Galgame? GetGalgameFromUid(GalgameUid? uid, GalgameUidFetchMode mode = GalgameUidFetchMode.Same);
    
    /// <summary>
    /// 获取某个uuid对应的游戏，若找不到则返回null
    /// </summary>
    /// <param name="uuid"></param>
    /// <returns></returns>
    public Galgame? GetGalgameFromUuid(Guid? uuid);

    /// <summary>
    /// 从id获取galgame
    /// </summary>
    /// <param name="id">id</param>
    /// <param name="rssType">id的信息源</param>
    /// <returns>galgame，若找不到返回null</returns>
    public Galgame? GetGalgameFromId(string? id, RssType rssType);

    /// <summary>
    /// 从名字获取galgame
    /// </summary>
    /// <param name="name">名字</param>
    /// <returns>galgame，找不到返回null</returns>
    public Galgame? GetGalgameFromName(string? name);

    /// <summary>
    /// 从信息源中搜刮游戏信息，会直接修改传入的galgame <br/>
    /// 会执行完整搜刮流程，包括从rss获取信息，用户确认，获取游玩状态等 <br/>
    /// 注意捕获异常
    /// </summary>
    /// <param name="galgame">待搜刮游戏</param>
    /// <param name="rssType">指定信息源，若为None则使用设置中的默认值</param>
    /// <param name="requireConfirm">是否需要用户确认解析信息</param>
    /// <returns>修改过后的galgame</returns>
    /// <exception cref="PvnException">要求用户确认解析信息且用户取消了该确认对话框</exception>
    public Task<Galgame> PhraseGalInfoAsync(Galgame galgame, RssType rssType = RssType.None, bool requireConfirm = false);
    
    /// <summary>
    /// 从信息源中搜刮游戏信息，只解析基本信息，直接修改传入的galgame
    /// </summary>
    /// <param name="galgame">待搜刮游戏</param>
    /// <param name="rssType">指定信息源，若为None则使用设置中的默认值</param>
    /// <returns>修改过后的游戏</returns>
    public Task<Galgame> PhraseGalInfoOnlyAsync(Galgame galgame, RssType rssType = RssType.None);

    /// <summary>
    /// 从信息源中搜刮游戏角色信息，直接修改传入的galgameCharacter
    /// </summary>
    /// <param name="galgameCharacter"></param>
    /// <param name="rssType"></param>
    /// <returns></returns>
    public Task<GalgameCharacter> PhraseGalCharacterAsync(GalgameCharacter galgameCharacter,
        RssType rssType = RssType.None);

    public Task ExportAsync(Action<string, int, int>? progress);
}