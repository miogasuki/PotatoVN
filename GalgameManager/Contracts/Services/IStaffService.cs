using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IStaffService
{
    /// <summary>
    /// 当某个游戏的staff列表发生变化时触发
    /// </summary>
    public event Action<Galgame> OnGameStaffChanged;
    
    public Task InitAsync();
    
    public Staff? GetStaff(Guid? id);
    
    /// <summary>
    /// 返回相似度最高的staff，如果相似度全为0则返回null<br/>
    /// 相似度计算方法见<see cref="StaffIdentifier.Match"/>
    /// </summary>
    /// <param name="identifier"></param>
    /// <returns></returns>
    public Staff? GetStaff(StaffIdentifier identifier);
    
    /// <summary>
    /// 获取某个galgame的staff列表
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public List<Staff> GetStaffs(Galgame game);

    /// <summary>
    /// 搜刮staff信息，直接修改传入的staff对象
    /// </summary>
    /// <param name="staff"></param>
    /// <param name="rss">信息源</param>
    /// <returns></returns>
    public Task<Staff> ParseStaffAsync(Staff staff, RssType rss);
    
    public void Save(Staff staff);
    
    public void Delete(Staff staff);
}