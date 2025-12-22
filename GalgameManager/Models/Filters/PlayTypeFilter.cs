using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Models.Filters;

public class PlayTypeFilter : FilterBase
{
    public PlayType PlayType { get; }

    public PlayTypeFilter(PlayType playType)
    {
        PlayType = playType;
        Name = playType.GetLocalized();
        SuggestName = $"{Name}/{"HomePage_PlayStatus".GetLocalized()}";
    }

    public override bool Apply(Galgame galgame) => galgame.PlayType == PlayType;

    public override string Name { get; }

    protected override string SuggestName { get; }
}
