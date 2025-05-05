using GalgameManager.Helpers.API.Steam;

namespace GalgameManager.Contracts.Services;

public interface ISteamService
{
    /// <summary>
    /// 提供一个初始化的方法
    /// </summary>
    /// <returns></returns>
    public Task InitAsync(string steamid);


    /// <summary>
    /// 取得steam账号
    /// </summary>
    /// 
    /// <returns></returns>
    public Task<SteamAccountDto> GetSteamAccountAsync();

    /// <summary>
    /// 通过steamid得到游戏列表的返回值
    /// </summary>
    /// <returns></returns>
    public Task<List<SteamGameDto>> GetSteamGameListResponseAsync();
}
