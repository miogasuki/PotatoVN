#nullable enable
using System.Diagnostics.CodeAnalysis;

namespace GalgameManager.Core.Helpers;

using TokenPathPair = (string Tag, string Path);

public readonly record struct TokenPathMapping
{
    private static readonly List<TokenPathPair> SysPaths;

    static TokenPathMapping()
    {
        SysPaths = [
            ( "%AppData%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) ),
            ( "%LocalAppData%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) ),
            ( "%Documents%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) ),
            ( "%UserProfile%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ),
        ];
        
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            SysPaths.Add(("%LocalLow%", localAppData.Replace("Local", "LocalLow")));
        }
    }
    
    public string? GameRoot { get; init;  }
    private TokenPathPair ? GameRootMapping => string.IsNullOrWhiteSpace(GameRoot) ? null : ("%GameRoot%", GameRoot);
    
    private IEnumerable<TokenPathPair> GetAllMappings()
    {
        foreach(TokenPathPair pathMapping in SysPaths)
            yield return pathMapping;
            
        if (GameRootMapping is { } m1) yield return m1;
    }

    public IEnumerable<(string Tag, string Path)> GetTagToPathMapping() => GetAllMappings()
        .OrderByDescending(m => m.Tag.Length);
    public IEnumerable<(string Path, string Tag)> GetPathToTagMapping() => GetAllMappings()
        .OrderByDescending(m => m.Path.Length)
        .Select(m => (m.Path, m.Tag));
}

public record struct GamePortablePath
{
    private TokenPathMapping Mapping { get; init;  }
    private string? ParsedPath { get; init;  }
    
    private string? _cachedPath;

    [return: NotNullIfNotNull(nameof(gamePortablePath.ParsedPath))]
    public static GamePortablePath? Create(GamePortablePath gamePortablePath, string? gameRoot)
    {
        return Create(gamePortablePath.ToPath(), gameRoot);
    }
    
    [return: NotNullIfNotNull(nameof(path))]
    public static GamePortablePath? Create(string? path, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        
        // 1. 展开任何可能的 tag
        path = _ToPath(path, new TokenPathMapping
        {
            GameRoot = gameRoot
        })!;
        
        // 2. 正则化相对路径
        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(gameRoot))
        {
            try 
            {
                path = Path.GetFullPath(Path.Combine(gameRoot, path));
            }
            catch { /* ignore an invalid path combination */ }
        }
        
        // 3. 重新用 Tag 压缩路径
        TokenPathMapping mapping = string.IsNullOrWhiteSpace(gameRoot)
            ? new TokenPathMapping()
            : new TokenPathMapping { GameRoot = gameRoot };

        foreach (var (fullpath, tag) in mapping.GetPathToTagMapping())
        {
            path = path.Replace(fullpath, tag, StringComparison.OrdinalIgnoreCase);
        }

        return new GamePortablePath { ParsedPath = path, Mapping = mapping };
    }

    /// <summary>
    /// 返回伪绝对路径（含标签）
    /// </summary>
    [return: NotNullIfNotNull(nameof(ParsedPath))]
    public string? ToDisplay() => ParsedPath;

    /// <summary>
    /// 返回真实的绝对路径
    /// </summary>
    [return: NotNullIfNotNull(nameof(ParsedPath))]
    public override string? ToString() => ToPath();
    
    /// <summary>
    /// 返回真实的绝对路径
    /// </summary>
    [return: NotNullIfNotNull(nameof(gamePortablePath.ParsedPath))]
    public static implicit operator string?(GamePortablePath gamePortablePath) => gamePortablePath.ToPath();
    

    /// <summary>
    /// 返回真实的绝对路径
    /// </summary>
    [return: NotNullIfNotNull(nameof(ParsedPath))]
    public string? ToPath() => _cachedPath ??= _ToPath(ParsedPath, Mapping);
    
    [return: NotNullIfNotNull(nameof(anyPath))]
    private static string? _ToPath(string? anyPath, TokenPathMapping anyMapping)
    {
        if (anyPath is null) return null;
        var path = anyPath;
        
        // TokenPathMapping newMapping = anyRoot is null 
        //     ? Mapping
        //     : Mapping with { GameRoot = anyRoot };
        
        foreach (var (tag, fullpath) in anyMapping.GetTagToPathMapping())
        {
            if (path.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                if (path.Length == tag.Length) return fullpath;
                
                var nextChar = path[tag.Length];
                if (nextChar == Path.DirectorySeparatorChar || nextChar == Path.AltDirectorySeparatorChar)
                {
                    var relative = path.Substring(tag.Length + 1);
                    return Path.Join(fullpath, relative);
                }
            }
        }

        return path;
    }
}