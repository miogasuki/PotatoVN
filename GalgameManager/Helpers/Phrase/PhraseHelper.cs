using System.Reflection;
using GalgameManager.Enums;
using GalgameManager.Models;
using NugetPackage;
using PotatoDBMapper.Models;
using SQLite;

namespace GalgameManager.Helpers.Phrase;

public static class PhraseHelper
{
    private const string DbFile = @"Assets\Data\vn_mapper.db";
    private static VnDbMapper? _vnDbMapper;
    private static Task? _unloadDbTask;
    private static bool _isUsing;

    private static void Init()
    {
        if (_vnDbMapper is not null) return;
        Assembly assembly = Assembly.GetExecutingAssembly();
        var file = Path.Combine(Path.GetDirectoryName(assembly.Location)!, DbFile);
        if (!File.Exists(file)) return;
        _vnDbMapper = new VnDbMapper();
        _vnDbMapper.Init(file);
        if (_unloadDbTask is not null)
            _unloadDbTask = Task.Run(async () =>
            {
                do
                {
                    await Task.Delay(1000 * 60 * 5); // 5 minutes
                    if (_isUsing) continue;
                    _vnDbMapper.Dispose();
                    _vnDbMapper = null;
                    _unloadDbTask = null;
                }while (_vnDbMapper is not null);
            });
    }

    public static async Task<int?> TryGetVndbIdAsync(string name) =>
        await TryGetMapAsync(name) is { } mapModel ? mapModel.VndbId : null;

    public static async Task<int?> TryGetBgmIdAsync(string name) =>
        await TryGetMapAsync(name) is { } mapModel ? mapModel.BgmId : null;
    
    public static async Task<int?> TryGetSteamIdAsync(string name) =>
        await TryGetMapAsync(name) is { } mapModel ? mapModel.SteamId : null;

    public static async Task<MapModel?> TryGetMapAsync(Galgame game)
    {
        _isUsing = true;
        Init();
        MapModel? result = null;
        if (!string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) 
            result ??= await _vnDbMapper!.TryGetMapAsync(VndbPhraser.GetId(game.Ids[(int)RssType.Vndb]!));
        if (!string.IsNullOrEmpty(game.Ids[(int)RssType.Bangumi]))
            result ??= (await _vnDbMapper!.TryGetMapsWithBgmId(Convert.ToInt32(game.Ids[(int)RssType.Bangumi])))
                .FirstOrDefault(map => map.BgmSimilarity >= 0.95);
        if (!string.IsNullOrEmpty(game.Name.Value))
            result ??= await TryGetMapAsync(game.Name.Value);
        _isUsing = false;
        return result;
    }
    
    private static async Task<MapModel?> TryGetMapAsync(string name)
    {
        _isUsing = true;
        Init();
        List<(MapModel model, double similarity)> result = await _vnDbMapper!.TryGetMapsWithName(name, 0.9);
        _isUsing = false;
        result.Sort((x, y) => x.similarity.CompareTo(y.similarity));
        if (result.Count > 0) return result[^1].model;
        return null;
    }
}

public static class ExVndb
{
    private const string DbFile = @"Assets\Data\ex-vndb.db";
    private static SQLiteAsyncConnection? _db;
    private static Task? _unloadDbTask;
    private static bool _isUsing;

    private static void Init()
    {
        if (_db is not null) return;
        Assembly assembly = Assembly.GetExecutingAssembly();
        var file = Path.Combine(Path.GetDirectoryName(assembly.Location)!, DbFile);
        if (!File.Exists(file)) return;
        _db = new SQLiteAsyncConnection(file);
        if (_unloadDbTask is not null)
            _unloadDbTask = Task.Run(async () =>
            {
                do
                {
                    await Task.Delay(1000 * 60 * 5); // 5 minutes
                    if (_isUsing) continue;
                    await _db.CloseAsync();
                    _db = null;
                    _unloadDbTask = null;
                }while (_db is not null);
            });
    }
    
    public static async Task<ExVn?> TryGetExVnAsync(string id)
    {
        _isUsing = true;
        Init();
        ExVn? result = await _db!.Table<ExVn>().Where(v => v.Id == id).FirstOrDefaultAsync();
        _isUsing = false;
        return result;
    }
    
    [Table("Vns")]
    public class ExVn
    {
        [MaxLength(50)]
        public string Id { get; set; } = null!;
    
        [MaxLength(50)]
        public string? BestHeaderImage { get; set; }
    
        [MaxLength(50)]
        public string? AlternativeHeaderImage { get; set; }
    
        [MaxLength(3)]
        public string? HeaderImageVersion { get; set; }
    }
}