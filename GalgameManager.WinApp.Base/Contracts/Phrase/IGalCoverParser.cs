using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

public interface IGalCoverParser
{
    /// <summary>
    /// 从rss中获取galgame的所有可能的封面图片
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <returns>所有可能的封面图片url列表，如果没有则返回空列表</returns>
    Task<List<string>> GetGalCoversAsync(Galgame galgame);
}