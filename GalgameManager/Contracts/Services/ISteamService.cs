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
    /// 这里是判断steam账号是否返回成功
    /// </summary>
    /// <param name="steamid"></param>
    /// <returns></returns>
    public bool CheckSteamAccountResponseDto(string steamid);

    /// <summary>
    /// 取得steam账号
    /// </summary>
    /// <param name="steamid"></param>
    /// <returns></returns>
    public SteamAccountDto GetSteamAccount(string steamid);

    /// <summary>
    /// 通过steamid得到游戏列表的返回值
    /// </summary>
    /// <param name="steamid"></param>
    /// <returns></returns>
    public SteamGameListResponseDto GetSteamGameListResponse(string steamid);

    /// <summary>
    /// 这里是通过appid来获得现在的游戏情况
    /// </summary>
    /// <param name="appid"></param>
    /// <returns></returns>
    public SteamGameDto GetSteamGameDto(string appid);
}
