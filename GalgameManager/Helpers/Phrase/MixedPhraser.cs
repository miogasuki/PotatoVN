using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Helpers.Phrase;

public class MixedPhraser : IGalInfoPhraser, IGalCharacterPhraser, IGalStaffParser
{
    private MixedPhraserData _data;
    private IEnumerable<string> _developerList;
    private bool _init;
    private Dictionary<RssType, IGalInfoPhraser?> _phrasers = new();

    private void Init()
    {
        _init = true;
        _developerList = ProducerDataHelper.Producers.SelectMany(p => p.Names);
    }
    
    private string? GetDeveloperFromTags(Galgame galgame)
    {
        if (!_init)
            Init();
        string? result = null;
        foreach (var tag in galgame.Tags.Value!)
        {
            double maxSimilarity = 0;
            foreach(var dev in _developerList)
            {
                if (IGalInfoPhraser.Similarity(dev, tag) > maxSimilarity)
                {
                    maxSimilarity = IGalInfoPhraser.Similarity(dev, tag);
                    result = dev;
                }
            }

            if (result != null && maxSimilarity > 0.75) // magic number: 一个tag和开发商的相似度大于0.75就认为是开发商
                break;
        }
        return result;
    }
    
    public MixedPhraser(BgmPhraser bgmPhraser, VndbPhraser vndbPhraser, YmgalPhraser ymgalPhraser, MixedPhraserData data)
    {
        _phrasers[RssType.Bangumi] = bgmPhraser;
        _phrasers[RssType.Vndb] = vndbPhraser;
        _phrasers[RssType.Ymgal] = ymgalPhraser;
        _data = data;
        _developerList = new List<string>();
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (!_init) Init();
        Dictionary<RssType, Task<Galgame?>?> phraserTasks = new ();
        foreach (RssType phraserType in RssTypeHelper.UsablePhrasers)
        {
            if (_phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) && phraser != null)
            {
                Galgame game = new() { Name = galgame.Name };
                game.RssType = phraserType;
                game.Id = galgame.Ids[(int)phraserType];
                phraserTasks[phraserType] = phraser.GetGalgameInfo(game);
            }
        }

        foreach (var (rssType, task) in phraserTasks)
        {
            try
            {
                if (task != null)
                    await task;
            }
            catch (Exception)
            {
                phraserTasks[rssType] = null;
            }
        }
        
        Dictionary<RssType, Galgame> metas = new();
        Galgame result = new();
        foreach (var (rssType, task) in phraserTasks)
        {
            if (task == null) continue;
            if (await task is { } game)
            {
                metas[rssType] = game;
                result.Ids[(int)rssType] = game.Id;
            }
            else
                result.Ids[(int)rssType] = null;
        }
        if(metas.Count == 0) return null;
        
        // 合并信息
        result.RssType = RssType.Mixed;
        result.UpdateMixedId();
        // name
        result.Name = GetValue(metas, nameof(Galgame.Name), _ => true, 
            new LockableProperty<string>(string.Empty));
        // description
        result.Description = GetValue(metas, nameof(Galgame.Description), 
            _ => true, new LockableProperty<string>(string.Empty));
        // expectedPlayTime
        result.ExpectedPlayTime = GetValue(metas, nameof(Galgame.ExpectedPlayTime), 
            meta => CheckStr(meta.ExpectedPlayTime.Value), 
            new LockableProperty<string>(Galgame.DefaultString));
        // rating
        result.Rating = GetValue(metas, nameof(Galgame.Rating), 
            _ => true, new LockableProperty<float>(0));
        // imageUrl
        result.ImageUrl = GetValue<string>(metas, nameof(Galgame.ImageUrl), 
            meta => CheckStr(meta.ImageUrl), null!);
        // release date
        result.ReleaseDate = GetValue(metas, nameof(Galgame.ReleaseDate),
            meta => meta.ReleaseDate.Value != DateTime.MinValue, 
            new LockableProperty<DateTime>(DateTime.MinValue));
        // characters
        result.Characters = GetValue(metas, nameof(Galgame.Characters),
            meta => meta.Characters.Count > 0, new ObservableCollection<GalgameCharacter>());
        // Chinese name
        result.CnName = GetValue(metas, nameof(Galgame.CnName),
            meta => CheckStr(meta.CnName), string.Empty);
        // developer
        result.Developer = GetValue(metas, nameof(Galgame.Developer),
            meta => CheckStr(meta.Developer), 
            new LockableProperty<string>(Galgame.DefaultString));
        // tags
        result.Tags = GetValue(metas, nameof(Galgame.Tags),
            meta => meta.Tags.Value?.Count > 0, 
            new LockableProperty<ObservableCollection<string>>(new ObservableCollection<string>()));
        
        // developer from tag
        if (result.Developer == Galgame.DefaultString)
        {
            var tmp = GetDeveloperFromTags(result);
            if (tmp != null)
                result.Developer = tmp;
        }
        return result;

        bool CheckStr(string? str) => !string.IsNullOrEmpty(str) && str != Galgame.DefaultString;
    }

    public void UpdateData(IGalInfoPhraserData data) => _data = (MixedPhraserData) data;

    public RssType GetPhraseType() => RssType.Mixed;

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        foreach (RssType phraserType in _data.Order.CharactersOrder)
        {
            if (galgameCharacter.Ids[(int)phraserType] != null &&
                _phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) &&
                phraser is IGalCharacterPhraser characterPhraser)
                return await characterPhraser.GetGalgameCharacter(galgameCharacter);
        }
        
        return null;
    }

    private T GetValue<T>(Dictionary<RssType, Galgame> metas, string propName, Func<Galgame, bool> isValueAvailable, 
        T defaultValue)
    {
        ObservableCollection<RssType> order = GetOrder();
        foreach (RssType rssType in order)
        {
            if(!metas.TryGetValue(rssType, out Galgame? meta)) continue;
            if (isValueAvailable(meta))
                return (T)(meta.GetType().GetProperty(propName)?.GetValue(meta) ??
                           meta.GetType().GetField(propName)?.GetValue(meta)!);
        }
        return defaultValue;
        
        ObservableCollection<RssType> GetOrder()
        {
            Type type = typeof(MixedPhraserOrder);
            PropertyInfo? prop =  type.GetProperty($"{propName}Order");
            Debug.Assert(prop != null, nameof(prop) + " != null");
            return (ObservableCollection<RssType>)prop.GetValue(_data.Order)!;
        }
    }

    public Task<Staff?> GetStaffAsync(Staff staff)
    {
        foreach (RssType phraserType in _data.Order.StaffOrder)
        {
            if (staff.Ids[(int)phraserType] != null &&
                _phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) &&
                phraser is IGalStaffParser staffParser)
                return staffParser.GetStaffAsync(staff);
        }
        return Task.FromResult<Staff?>(null);
    }

    public Task<List<StaffRelation>> GetStaffsAsync(Galgame game)
    {
        foreach (RssType phraserType in _data.Order.StaffOrder)
        {
            if (game.Ids[(int)phraserType] != null &&
                _phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) &&
                phraser is IGalStaffParser staffParser)
                return staffParser.GetStaffsAsync(game);
        }
        return Task.FromResult(new List<StaffRelation>());
    }
}

public class MixedPhraserOrder
{
    // 版本号，每次添加新搜刮器/添加新字段的时候都应该把这个数字+1，以便galgameCollectionService能够更新配置中已有的顺序配置
    // 更新配置不需要手动编写，已经在GalgameCollectionService中使用反射实现，会自动添加新的默认配置
    public const int Version = 6;
    
    // 为什么使用ObservableCollection：为了能够在MixedPhraserOrderDialog中使顺序能够drag&drop
    // 所有变量都应该命名为：{字段名}Order，此处字段名应该与Galgame中对应的字段名一致（为了让GetValue中的反射能够找到对应的字段）
    public ObservableCollection<RssType> NameOrder { get; set; } = new();
    public ObservableCollection<RssType> DescriptionOrder { get; set; } = new();
    public ObservableCollection<RssType> ExpectedPlayTimeOrder { get; set; } = new();
    public ObservableCollection<RssType> RatingOrder { get; set; } = new();
    public ObservableCollection<RssType> ImageUrlOrder { get; set; } = new();
    public ObservableCollection<RssType> ReleaseDateOrder { get; set; } = new();
    public ObservableCollection<RssType> CharactersOrder { get; set; } = new();
    public ObservableCollection<RssType> CnNameOrder { get; set; } = new();
    public ObservableCollection<RssType> DeveloperOrder { get; set; } = new();
    public ObservableCollection<RssType> TagsOrder { get; set; } = new();
    public ObservableCollection<RssType> StaffOrder { get; set; } = new();

    public MixedPhraserOrder SetToDefault()
    {
        NameOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        DescriptionOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        ExpectedPlayTimeOrder = new() { RssType.Vndb};
        RatingOrder = new() { RssType.Bangumi, RssType.Vndb };
        ImageUrlOrder = new() { RssType.Vndb, RssType.Bangumi, RssType.Ymgal };
        ReleaseDateOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        CharactersOrder = new() { RssType.Bangumi, RssType.Vndb };
        CnNameOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        DeveloperOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        TagsOrder = new() { RssType.Bangumi, RssType.Vndb };
        StaffOrder = new() { RssType.Bangumi, RssType.Vndb};
        return this;
    }
}

public class MixedPhraserData : IGalInfoPhraserData
{
    public required MixedPhraserOrder Order { get; init; }
}