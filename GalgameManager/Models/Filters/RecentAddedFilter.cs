using System;
using GalgameManager.Helpers;

namespace GalgameManager.Models.Filters;

public class RecentAddedFilter : FilterBase
{
    private readonly int _days;

    public RecentAddedFilter(int days = 30)
    {
        _days = days;
        Name = "HomePage_Filter_RecentAdded".GetLocalized();
        SuggestName = Name;
    }

    public override bool Apply(Galgame galgame)
    {
        if (galgame.AddTime == DateTime.MinValue)
            return false;

        return galgame.AddTime >= DateTime.Now.AddDays(-_days);
    }

    public override string Name { get; }

    protected override string SuggestName { get; }
}
