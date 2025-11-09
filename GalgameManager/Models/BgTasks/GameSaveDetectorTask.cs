using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using GalgameManager.Helpers;

namespace GalgameManager.Models.BgTasks;

public class GameSaveDetectorTask : BgTaskBase
{
    public Galgame? Galgame { get; set; }
    public List<string> DetectedSavePaths { get; set; } = new();
    public List<string> MonitoredPaths { get; set; } = new();
    public bool IsMonitoring { get; set; }
    public int SaveOperationCount { get; set; }
    public bool UseEverythingSearch { get; set; } = true; // 优先使用Everything搜索

    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly List<string> _candidatePaths = new();
    private readonly List<string> _pendingMonitorPaths = new(); // 待监听的路径
    private readonly Dictionary<string, DateTime> _pathFirstDetected = new(); // 路径首次检测时间
    private DateTime _monitorStartTime;
    private const int DELAY_SECONDS = 10; // 延迟10秒后开始监听不存在的路径
    private const int SAVE_COUNT_THRESHOLD = 3; // 需要检测到3次保存操作才开始监听

    // Everything.dll API declarations (基于官方SDK)
    [DllImport("Everything.dll", CharSet = CharSet.Unicode)]
    private static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport("Everything.dll")]
    private static extern void Everything_SetMatchPath(bool bEnable);

    [DllImport("Everything.dll")]
    private static extern void Everything_SetMatchCase(bool bEnable);

    [DllImport("Everything.dll")]
    private static extern void Everything_SetWholeWord(bool bEnable);

    [DllImport("Everything.dll")]
    private static extern bool Everything_QueryW(bool bWait);

    [DllImport("Everything.dll")]
    private static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpFileName, int nMaxChars);

    [DllImport("Everything.dll")]
    private static extern uint Everything_GetNumResults();

    [DllImport("Everything.dll")]
    private static extern bool Everything_IsAppLoaded();

    [DllImport("Everything.dll")]
    private static extern void Everything_Reset();

    public GameSaveDetectorTask() { } // For serialization

    public GameSaveDetectorTask(Galgame game)
    {
        Galgame = game;
        InitializeCandidatePaths();
    }

    private void InitializeCandidatePaths()
    {
        if (Galgame == null) return;

        Debug.WriteLine("[GameSaveDetector] 初始化候选路径");

        // 游戏安装目录（如果是本地的）
        if (!string.IsNullOrEmpty(Galgame.LocalPath))
        {
            _candidatePaths.Add(Galgame.LocalPath);
            Debug.WriteLine($"[GameSaveDetector] 添加游戏安装目录: {Galgame.LocalPath}");
        }

        // 用户文档目录
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _candidatePaths.Add(documentsPath);
        _candidatePaths.Add(Path.Combine(documentsPath, "My Games"));
        _candidatePaths.Add(Path.Combine(documentsPath, "Saved Games"));

        // AppData 目录
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localLowPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("Local", "LocalLow");

        _candidatePaths.Add(appDataPath);
        _candidatePaths.Add(localAppDataPath);
        _candidatePaths.Add(localLowPath);

        // 获取当前程序路径以排除 PotatoVN 相关路径
        var currentAppPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        Debug.WriteLine($"[GameSaveDetector] 当前程序路径: {currentAppPath}");

        // 基于游戏名称和开发者的启发式路径
        var gameKeywords = ExtractGameKeywords();
        Debug.WriteLine($"[GameSaveDetector] 提取到的关键词数量: {gameKeywords.Count}");

        foreach (var keyword in gameKeywords)
        {
            if (!string.IsNullOrEmpty(keyword))
            {
                var combinedAppDataPath = Path.Combine(appDataPath, keyword);
                var combinedLocalAppDataPath = Path.Combine(localAppDataPath, keyword);
                var combinedDocumentsPath = Path.Combine(documentsPath, keyword);

                // 检查路径是否包含 PotatoVN 或在当前程序路径下
                if (!ShouldExcludePath(combinedAppDataPath, currentAppPath))
                {
                    _candidatePaths.Add(combinedAppDataPath);
                    Debug.WriteLine($"[GameSaveDetector] 添加AppData路径: {combinedAppDataPath}");
                }

                if (!ShouldExcludePath(combinedLocalAppDataPath, currentAppPath))
                {
                    _candidatePaths.Add(combinedLocalAppDataPath);
                    Debug.WriteLine($"[GameSaveDetector] 添加LocalAppData路径: {combinedLocalAppDataPath}");
                }

                if (!ShouldExcludePath(combinedDocumentsPath, currentAppPath))
                {
                    _candidatePaths.Add(combinedDocumentsPath);
                    Debug.WriteLine($"[GameSaveDetector] 添加文档路径: {combinedDocumentsPath}");
                }
            }
        }

        Debug.WriteLine($"[GameSaveDetector] 最终候选路径数量: {_candidatePaths.Count}");
    }

    private bool ShouldExcludePath(string targetPath, string currentAppPath)
    {
        if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(currentAppPath))
            return false;

        var targetLower = targetPath.ToLowerInvariant();
        var appPathLower = currentAppPath.ToLowerInvariant();

        // 排除包含 .PotatoVN 的路径
        if (targetLower.Contains(".potatovn") || targetLower.Contains("potatovn"))
        {
            Debug.WriteLine($"[GameSaveDetector] 排除PotatoVN路径: {targetPath}");
            return true;
        }

        // 排除在当前程序路径下的路径
        if (targetPath.StartsWith(appPathLower, StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"[GameSaveDetector] 排除程序路径下: {targetPath}");
            return true;
        }

        return false;
    }

    private List<string> ExtractGameKeywords()
    {
        var keywords = new List<string>();

        if (Galgame == null) return keywords;

        // 从游戏名称提取关键字
        if (Galgame.Name?.Value is { } gameNameValue)
        {
            keywords.Add(gameNameValue);
        }

        if (!string.IsNullOrEmpty(Galgame.ChineseName))
        {
            keywords.Add(Galgame.ChineseName!);
        }

        if (Galgame.OriginalName?.Value is { } originalNameValue)
        {
            keywords.Add(originalNameValue);
        }

        // 从开发者名称提取关键字
        if (!string.IsNullOrEmpty(Galgame.Developer?.Value))
        {
            keywords.Add(Galgame.Developer.Value);
        }

        // 从分类中提取关键字
        if (Galgame.Categories != null)
        {
            foreach (var category in Galgame.Categories)
            {
                if (category.Name != null)
                {
                    keywords.Add(category.Name);
                }
            }
        }

        // 添加开发者变体，特别是针对 ASa Project 的各种可能性
        AddDeveloperVariants(keywords);

        return keywords;
    }

    /// <summary>
    /// 生成游戏相关的所有变体关键词
    /// </summary>
    /// <param name="game">游戏对象</param>
    /// <returns>包含所有变体的列表</returns>
    private List<string> GenerateAllVariants(Galgame game)
    {
        var allVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (game == null) return allVariants.ToList();

        Debug.WriteLine($"[GameSaveDetector] 开始为游戏 '{game.Name?.Value}' 生成变体");

        // 1. 游戏名称变体
        GenerateNameVariants(game.Name?.Value, allVariants);
        GenerateNameVariants(game.ChineseName, allVariants);
        GenerateNameVariants(game.OriginalName?.Value, allVariants);

        // 2. 开发者变体
        GenerateDeveloperVariants(game.Developer?.Value, allVariants);

        // 3. 分类变体
        if (game.Categories != null)
        {
            foreach (var category in game.Categories)
            {
                if (!string.IsNullOrEmpty(category.Name))
                {
                    GenerateNameVariants(category.Name, allVariants);
                }
            }
        }

        Debug.WriteLine($"[GameSaveDetector] 总共生成了 {allVariants.Count} 个变体");
        return allVariants.ToList();
    }

    /// <summary>
    /// 生成名称的所有变体
    /// </summary>
    private void GenerateNameVariants(string? name, HashSet<string> variants)
    {
        if (string.IsNullOrEmpty(name)) return;

        Debug.WriteLine($"[GameSaveDetector] 为名称 '{name}' 生成变体");

        // 原始名称
        variants.Add(name);

        // 大小写变体
        variants.Add(name.ToLowerInvariant());
        variants.Add(name.ToUpperInvariant());

        // 首字母大写
        if (name.Length > 0)
        {
            var titleCase = char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();
            variants.Add(titleCase);
        }

        // 空格和分隔符变体
        GenerateSeparatorVariants(name, variants);

        // 常见缩写和简化变体
        GenerateAbbreviationVariants(name, variants);

        // 特殊字符处理
        GenerateSpecialCharacterVariants(name, variants);
    }

    /// <summary>
    /// 生成分隔符变体（空格、下划线、连字符等）
    /// </summary>
    private void GenerateSeparatorVariants(string name, HashSet<string> variants)
    {
        var separators = new[] { " ", "_", "-", ".", "" };
        var currentSeparators = new[] { " ", "_", "-", "." };

        foreach (var currentSep in currentSeparators)
        {
            if (!name.Contains(currentSep)) continue;

            var parts = name.Split(new[] { currentSep }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            // 为每个分隔符生成交替版本
            foreach (var newSep in separators)
            {
                if (newSep == currentSep) continue;

                var variant = string.Join(newSep, parts);
                variants.Add(variant);
            }

            // 无分隔符版本
            var noSepVariant = string.Join("", parts);
            variants.Add(noSepVariant);

            // 驼峰命名版本
            var camelCaseVariant = string.Join("", parts.Select((part, index) =>
                index == 0 ? part.ToLowerInvariant() :
                (part.Length > 0 ? char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant() : part)));
            variants.Add(camelCaseVariant);
        }
    }

    /// <summary>
    /// 生成缩写和简化变体
    /// </summary>
    private void GenerateAbbreviationVariants(string name, HashSet<string> variants)
    {
        // 提取首字母缩写
        var words = name.Split(new[] { " ", "_", "-", "." }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var abbreviation = new string(words.Select(word =>
                string.IsNullOrEmpty(word) ? ' ' : char.ToUpperInvariant(word[0])).ToArray());
            variants.Add(abbreviation);
            variants.Add(abbreviation.ToLowerInvariant());
        }

        // 常见词汇的简化
        var simplifications = new Dictionary<string, string[]>
        {
            { "project", new[] { "proj", "p" } },
            { "game", new[] { "gm" } },
            { "visual", new[] { "vis" } },
            { "novel", new[] { "vn", "nov" } },
            { "story", new[] { "stry", "st" } },
            { "adventure", new[] { "adv", "adven" } },
            { "chronicles", new[] { "chron", "chr" } },
            { "legend", new[] { "leg", "lgd" } },
            { "fantasy", new[] { "fan", "fnt" } },
            { "world", new[] { "wrld", "wd" } }
        };

        var lowerName = name.ToLowerInvariant();
        foreach (var simplification in simplifications)
        {
            if (lowerName.Contains(simplification.Key))
            {
                foreach (var replacement in simplification.Value)
                {
                    var simplified = lowerName.Replace(simplification.Key, replacement);
                    variants.Add(simplified);
                    variants.Add(simplified.Replace("_", "").Replace("-", "").Replace(" ", ""));
                }
            }
        }
    }

    /// <summary>
    /// 生成特殊字符变体
    /// </summary>
    private void GenerateSpecialCharacterVariants(string name, HashSet<string> variants)
    {
        // 日语字符的罗马音变体
        var japaneseVariants = GenerateJapaneseVariants(name);
        foreach (var variant in japaneseVariants)
        {
            variants.Add(variant);
        }

        // 数字变体（阿拉伯数字 vs 中文数字）
        var numberVariants = GenerateNumberVariants(name);
        foreach (var variant in numberVariants)
        {
            variants.Add(variant);
        }
    }

    /// <summary>
    /// 生成日语相关变体
    /// </summary>
    private List<string> GenerateJapaneseVariants(string name)
    {
        var variants = new List<string>();

        // 常见日语词汇的罗马音
        var japaneseMappings = new Dictionary<string, string[]>
        {
            { "プロジェクト", new[] { "project", "purojekuto" } },
            { "ウォーズ", new[] { "wars", "waruzu" } },
            { "ストーリー", new[] { "story", "story", "sutori" } },
            { "ファンタジー", new[] { "fantasy", "fantesi" } },
            { "アドベンチャー", new[] { "adventure", "adobencha" } },
            { "クロニクル", new[] { "chronicle", "kuronikuru" } }
        };

        var lowerName = name.ToLowerInvariant();
        foreach (var mapping in japaneseMappings)
        {
            if (name.Contains(mapping.Key))
            {
                foreach (var variant in mapping.Value)
                {
                    variants.Add(lowerName.Replace(mapping.Key, variant));
                }
            }
        }

        return variants;
    }

    /// <summary>
    /// 生成数字变体
    /// </summary>
    private List<string> GenerateNumberVariants(string name)
    {
        var variants = new List<string>();

        var numberMappings = new Dictionary<string, string>
        {
            { "0", "零" }, { "1", "一" }, { "2", "二" }, { "3", "三" },
            { "4", "四" }, { "5", "五" }, { "6", "六" }, { "7", "七" },
            { "8", "八" }, { "9", "九" }, { "10", "十" }
        };

        var result = name;
        foreach (var mapping in numberMappings)
        {
            result = result.Replace(mapping.Key, mapping.Value);
            variants.Add(result);
        }

        return variants;
    }

    /// <summary>
    /// 生成开发者特定的变体
    /// </summary>
    private void GenerateDeveloperVariants(string? developer, HashSet<string> variants)
    {
        if (string.IsNullOrEmpty(developer)) return;

        Debug.WriteLine($"[GameSaveDetector] 为开发者 '{developer}' 生成变体");

        // 基础名称变体
        GenerateNameVariants(developer, variants);

        // 特定开发者的已知变体
        var lowerDev = developer.ToLowerInvariant();
        var developerSpecificVariants = GenerateKnownDeveloperVariants(lowerDev);

        foreach (var variant in developerSpecificVariants)
        {
            variants.Add(variant);
        }
    }

    /// <summary>
    /// 为已知开发者生成特定变体
    /// </summary>
    private List<string> GenerateKnownDeveloperVariants(string developer)
    {
        var variants = new List<string>();

        // ASa Project 特定变体
        if (developer.Contains("asa") || developer.Contains("asaproject"))
        {
            Debug.WriteLine("[GameSaveDetector] 检测到 ASa Project，添加特定变体");
            variants.AddRange(new[]
            {
                "asaproject", "asa project", "asa_project", "asa-project",
                "AsaProject", "ASA Project", "ASA_PROJECT", "asaproj",
                "asa_proj", "asa-proj", "AsaProj", "asaprojects",
                "asa projects", "asa_projects", "asa-projects"
            });
        }

        // 其他知名开发者的变体可以在这里添加
        if (developer.Contains("key") || developer.Contains("visualarts"))
        {
            variants.AddRange(new[] { "key", "visualarts", "visual arts", "visual_arts" });
        }

        if (developer.Contains("type-moon") || developer.Contains("typemoon"))
        {
            variants.AddRange(new[] { "typemoon", "type-moon", "type_moon" });
        }

        return variants;
    }

    private void AddDeveloperVariants(List<string> keywords)
    {
        // 重构为使用新的变体生成系统
        var allVariants = GenerateAllVariants(Galgame!);

        foreach (var variant in allVariants)
        {
            if (!string.IsNullOrEmpty(variant) && !keywords.Contains(variant))
            {
                keywords.Add(variant);
            }
        }
    }

    private void AddGameNameVariants(List<string> keywords, string gameName)
    {
        // 保持向后兼容性，但内部使用新系统
        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        GenerateNameVariants(gameName, variants);

        foreach (var variant in variants)
        {
            if (!keywords.Contains(variant))
            {
                keywords.Add(variant);
            }
        }
    }

    protected override Task RecoverFromJsonInternal()
    {
        // 重新初始化候选路径
        InitializeCandidatePaths();
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        if (Galgame == null) return;

        Debug.WriteLine($"[GameSaveDetector] 开始为游戏 '{Galgame.Name?.Value}' 检测存档路径");
        Debug.WriteLine($"[GameSaveDetector] 候选路径数量: {_candidatePaths.Count}");

        if (UseEverythingSearch && TryEverythingSearch())
        {
            Debug.WriteLine($"[GameSaveDetector] 使用Everything搜索找到 {DetectedSavePaths.Count} 个潜在存档文件");

            // 即使Everything找到了文件，也继续监听一段时间以发现新的保存操作
            await StartDelayedFileSystemMonitoring();
        }
        else
        {
            Debug.WriteLine("[GameSaveDetector] Everything不可用或禁用，回退到文件系统监听");
            await StartDelayedFileSystemMonitoring();
        }

        // 设置存档目录
        var finalPaths = FilterDetectedPaths();
        Debug.WriteLine($"[GameSaveDetector] 过滤后的候选路径数量: {finalPaths.Count}");

        if (finalPaths.Count > 0 && Galgame != null)
        {
            var saveDirectory = FindBestSaveDirectory(finalPaths);
            Debug.WriteLine($"[GameSaveDetector] 最终选择的存档目录: {saveDirectory}");

            if (!string.IsNullOrEmpty(saveDirectory))
            {
                Galgame.DetectedSavePosition = saveDirectory;
            }
            else
            {
                var fallbackDirectory = Path.GetDirectoryName(finalPaths[0]);
                if (!string.IsNullOrEmpty(fallbackDirectory))
                {
                    Galgame.DetectedSavePosition = fallbackDirectory;
                    Debug.WriteLine($"[GameSaveDetector] 使用回退目录: {fallbackDirectory}");
                }
            }
        }
        else
        {
            Debug.WriteLine("[GameSaveDetector] 未找到合适的存档目录");
        }
    }

    private bool TryEverythingSearch()
    {
        try
        {
            Debug.WriteLine("[GameSaveDetector] 尝试使用Everything.dll进行搜索");

            // 检查Everything是否可用
            if (!Everything_IsAppLoaded())
            {
                Debug.WriteLine("[GameSaveDetector] Everything未运行，无法使用 Everything.dll");
                return false;
            }

            Debug.WriteLine("[GameSaveDetector] Everything正在运行，开始搜索存档文件");

            var saveExtensions = new[] { "sav", "dat", "save", "sfs", "rpgsave", "rvdata", "rvdata2" };
            var saveKeywords = new[] { "save", "sav", "slot", "data", "record", "progress", "file" };
            var gameKeywords = ExtractGameKeywords();

            var totalFound = 0;

            // 按文件扩展名搜索
            foreach (var ext in saveExtensions)
            {
                try
                {
                    Everything_Reset();

                    // 设置搜索参数
                    Everything_SetSearchW($"ext:{ext}");
                    Everything_SetMatchPath(true); // 启用路径匹配
                    Everything_SetMatchCase(false); // 忽略大小写

                    if (Everything_QueryW(true)) // 等待查询完成
                    {
                        var resultCount = Everything_GetNumResults();
                        Debug.WriteLine($"[GameSaveDetector] 搜索 ext:{ext} 找到 {resultCount} 个文件");

                        for (uint i = 0; i < resultCount; i++)
                        {
                            var fullPath = new StringBuilder(1024);
                            Everything_GetResultFullPathNameW(i, fullPath, fullPath.Capacity);

                            var path = fullPath.ToString();
                            if (!string.IsNullOrEmpty(path) && IsPotentialSaveFile(path))
                            {
                                lock (DetectedSavePaths)
                                {
                                    if (!DetectedSavePaths.Contains(path))
                                    {
                                        DetectedSavePaths.Add(path);
                                        totalFound++;
                                        Debug.WriteLine($"[GameSaveDetector] Everything找到存档文件: {path}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GameSaveDetector] Everything搜索 ext:{ext} 时出错: {ex.Message}");
                }
            }

            // 按关键词搜索文件名
            foreach (var keyword in saveKeywords)
            {
                try
                {
                    Everything_Reset();

                    Everything_SetSearchW($"*{keyword}*");
                    Everything_SetMatchPath(true);
                    Everything_SetMatchCase(false);

                    if (Everything_QueryW(true))
                    {
                        var resultCount = Everything_GetNumResults();
                        Debug.WriteLine($"[GameSaveDetector] 搜索关键词 '*{keyword}*' 找到 {resultCount} 个文件");

                        for (uint i = 0; i < resultCount; i++)
                        {
                            var fullPath = new StringBuilder(1024);
                            Everything_GetResultFullPathNameW(i, fullPath, fullPath.Capacity);

                            var path = fullPath.ToString();
                            if (!string.IsNullOrEmpty(path) && IsPotentialSaveFile(path))
                            {
                                lock (DetectedSavePaths)
                                {
                                    if (!DetectedSavePaths.Contains(path))
                                    {
                                        DetectedSavePaths.Add(path);
                                        totalFound++;
                                        Debug.WriteLine($"[GameSaveDetector] Everything关键词搜索找到存档文件: {path}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GameSaveDetector] Everything关键词搜索 '{keyword}' 时出错: {ex.Message}");
                }
            }

            // 按游戏特定关键词搜索路径
            foreach (var keyword in gameKeywords)
            {
                if (string.IsNullOrEmpty(keyword)) continue;

                try
                {
                    Everything_Reset();

                    // 搜索包含游戏关键词的路径
                    Everything_SetSearchW($"*{keyword}*");
                    Everything_SetMatchPath(true);
                    Everything_SetMatchCase(false);

                    if (Everything_QueryW(true))
                    {
                        var resultCount = Everything_GetNumResults();
                        Debug.WriteLine($"[GameSaveDetector] 搜索游戏关键词 '*{keyword}*' 找到 {resultCount} 个文件");

                        for (uint i = 0; i < resultCount; i++)
                        {
                            var fullPath = new StringBuilder(1024);
                            Everything_GetResultFullPathNameW(i, fullPath, fullPath.Capacity);

                            var path = fullPath.ToString();
                            if (!string.IsNullOrEmpty(path) && IsPotentialSaveFile(path))
                            {
                                lock (DetectedSavePaths)
                                {
                                    if (!DetectedSavePaths.Contains(path))
                                    {
                                        DetectedSavePaths.Add(path);
                                        totalFound++;
                                        Debug.WriteLine($"[GameSaveDetector] Everything游戏关键词搜索找到存档文件: {path}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GameSaveDetector] Everything游戏关键词搜索 '{keyword}' 时出错: {ex.Message}");
                }
            }

            Debug.WriteLine($"[GameSaveDetector] Everything搜索完成，总共找到 {totalFound} 个潜在存档文件");
            return totalFound > 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] Everything.dll调用失败: {ex.Message}");
            return false;
        }
    }

    private async Task StartDelayedFileSystemMonitoring()
    {
        IsMonitoring = true;
        Debug.WriteLine("[GameSaveDetector] 开始延迟文件系统监听");

        // 首先监听已存在的路径
        StartMonitoringForExistingPaths();

        // 监听最多2分钟，包含延迟机制
        var maxMonitorTime = TimeSpan.FromMinutes(2);
        var earlyStopThreshold = 3;
        var confidenceThreshold = 2;

        _monitorStartTime = DateTime.Now;
        Debug.WriteLine($"[GameSaveDetector] 开始文件系统监听，最长监听时间: {maxMonitorTime.TotalMinutes} 分钟");

        while (IsMonitoring && (DateTime.Now - _monitorStartTime) < maxMonitorTime)
        {
            Debug.WriteLine($"[GameSaveDetector] 当前检测到 {DetectedSavePaths.Count} 个潜在存档文件");

            // 检查是否有路径可以开始监听
            await CheckAndStartPendingMonitors();

            if (ShouldStopEarly(DetectedSavePaths, earlyStopThreshold, confidenceThreshold))
            {
                Debug.WriteLine("[GameSaveDetector] 达到早停条件，停止监听");
                break;
            }

            await Task.Delay(1000); // 每秒检查一次
        }

        StopFileSystemMonitoring();
    }

    private void StartMonitoringForExistingPaths()
    {
        foreach (var path in _candidatePaths)
        {
            if (Directory.Exists(path))
            {
                CreateFileSystemWatcher(path);
            }
            else
            {
                _pendingMonitorPaths.Add(path);
                Debug.WriteLine($"[GameSaveDetector] 路径不存在，加入待监听列表: {path}");
            }
        }

        Debug.WriteLine($"[GameSaveDetector] 立即监听 {_watchers.Count} 个已存在路径，{_pendingMonitorPaths.Count} 个路径待监听");
    }

    private async Task CheckAndStartPendingMonitors()
    {
        var pathsToMonitor = new List<string>();

        foreach (var pendingPath in _pendingMonitorPaths.ToList())
        {
            if (Directory.Exists(pendingPath))
            {
                if (!_pathFirstDetected.ContainsKey(pendingPath))
                {
                    _pathFirstDetected[pendingPath] = DateTime.Now;
                    Debug.WriteLine($"[GameSaveDetector] 路径首次出现，加入延迟列表: {pendingPath}");
                }
                else if ((DateTime.Now - _pathFirstDetected[pendingPath]).TotalSeconds >= DELAY_SECONDS)
                {
                    pathsToMonitor.Add(pendingPath);
                    _pendingMonitorPaths.Remove(pendingPath);
                    _pathFirstDetected.Remove(pendingPath);
                    Debug.WriteLine($"[GameSaveDetector] 延迟时间到，准备监听路径: {pendingPath}");
                }
            }
        }

        // 检查是否有路径因为检测到保存操作而需要提前开始监听
        if (SaveOperationCount >= SAVE_COUNT_THRESHOLD)
        {
            foreach (var pendingPath in _pendingMonitorPaths.ToList())
            {
                if (Directory.Exists(pendingPath))
                {
                    pathsToMonitor.Add(pendingPath);
                    _pendingMonitorPaths.Remove(pendingPath);
                    _pathFirstDetected.Remove(pendingPath);
                    Debug.WriteLine($"[GameSaveDetector] 检测到足够的保存操作，提前监听路径: {pendingPath}");
                }
            }
        }

        // 开始监听满足条件的路径
        foreach (var path in pathsToMonitor)
        {
            CreateFileSystemWatcher(path);
        }

        // 如果没有监听器但有待监听路径，尝试通过Everything检测新创建的路径
        if (_watchers.Count == 0 && _pendingMonitorPaths.Count > 0 && UseEverythingSearch)
        {
            await TryDetectNewPathsWithEverything();
        }
    }

    private async Task TryDetectNewPathsWithEverything()
    {
        if (!Everything_IsAppLoaded()) return;

        try
        {
            var gameKeywords = ExtractGameKeywords();
            foreach (var keyword in gameKeywords)
            {
                if (string.IsNullOrEmpty(keyword)) continue;

                Everything_Reset();
                Everything_SetSearchW($"*{keyword}*");
                Everything_SetMatchPath(true);
                Everything_SetMatchCase(false);

                if (Everything_QueryW(false)) // 不等待，快速检查
                {
                    var resultCount = Everything_GetNumResults();
                    for (uint i = 0; i < resultCount; i++)
                    {
                        var fullPath = new StringBuilder(1024);
                        Everything_GetResultFullPathNameW(i, fullPath, fullPath.Capacity);
                        var path = fullPath.ToString();

                        var directory = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(directory) && _pendingMonitorPaths.Contains(directory))
                        {
                            Debug.WriteLine($"[GameSaveDetector] Everything检测到新路径的文件，提前开始监听: {directory}");
                            if (Directory.Exists(directory))
                            {
                                CreateFileSystemWatcher(directory);
                                _pendingMonitorPaths.Remove(directory);
                            }
                        }
                    }
                }

                await Task.Delay(100); // 避免过于频繁的Everything查询
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] Everything检测新路径时出错: {ex.Message}");
        }
    }

    private void CreateFileSystemWatcher(string path)
    {
        try
        {
            Debug.WriteLine($"[GameSaveDetector] 设置监听器监控路径: {path}");
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                // 减少缓冲区大小以提高响应速度
                InternalBufferSize = 4096,
                // 通知所有变化
                NotifyFilter = NotifyFilters.FileName |
                              NotifyFilters.LastWrite |
                              NotifyFilters.Size |
                              NotifyFilters.Attributes |
                              NotifyFilters.CreationTime
            };

            // 监听所有相关事件
            watcher.Created += OnFileSystemChanged;
            watcher.Changed += OnFileSystemChanged;
            watcher.Renamed += OnFileSystemChanged;

            _watchers.Add(watcher);
            MonitoredPaths.Add(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] 监听路径 {path} 失败: {ex.Message}");
        }
    }

    
    private void StopFileSystemMonitoring()
    {
        IsMonitoring = false;
        Debug.WriteLine($"[GameSaveDetector] 停止文件系统监听，清理 {_watchers.Count} 个监听器");

        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                Debug.WriteLine("[GameSaveDetector] 成功清理一个文件系统监听器");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameSaveDetector] 清理监听器时出错: {ex.Message}");
            }
        }

        _watchers.Clear();
        MonitoredPaths.Clear();
        Debug.WriteLine("[GameSaveDetector] 文件系统监听已完全停止");
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsMonitoring) return;

        try
        {
            Debug.WriteLine($"[GameSaveDetector] 检测到文件系统变化: {e.ChangeType} - {e.FullPath}");

            // 处理重命名事件
            if (e is RenamedEventArgs renamedArgs)
            {
                Debug.WriteLine($"[GameSaveDetector] 文件重命名: {renamedArgs.OldFullPath} -> {renamedArgs.FullPath}");
                if (IsPotentialSaveFile(renamedArgs.FullPath))
                {
                    lock (DetectedSavePaths)
                    {
                        if (!DetectedSavePaths.Contains(renamedArgs.FullPath))
                        {
                            DetectedSavePaths.Add(renamedArgs.FullPath);
                            SaveOperationCount++;
                            Debug.WriteLine($"[GameSaveDetector] 发现新的存档文件 (重命名): {renamedArgs.FullPath}");
                        }
                    }
                }
            }
            else
            {
                // 检查文件是否可能是存档文件
                if (IsPotentialSaveFile(e.FullPath))
                {
                    lock (DetectedSavePaths)
                    {
                        if (!DetectedSavePaths.Contains(e.FullPath))
                        {
                            DetectedSavePaths.Add(e.FullPath);
                            SaveOperationCount++;
                            Debug.WriteLine($"[GameSaveDetector] 发现新的存档文件 ({e.ChangeType}): {e.FullPath}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] 处理文件系统变化时出错: {ex.Message}");
        }
    }

    private bool IsPotentialSaveFile(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            // 检查文件扩展名（常见的存档文件扩展名）
            var saveExtensions = new[] { ".sav", ".dat", ".save", ".sfs", ".rpgsave", ".rvdata", ".rvdata2" };
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (saveExtensions.Contains(extension))
            {
                Debug.WriteLine($"[GameSaveDetector] 文件扩展名匹配: {fileName} (.{extension})");
                return true;
            }

            // 检查文件名是否包含存档相关关键词
            var saveKeywords = new[] { "save", "sav", "slot", "data", "record", "progress", "file" };
            var fileNameLower = fileName.ToLowerInvariant();

            foreach (var keyword in saveKeywords)
            {
                if (fileNameLower.Contains(keyword))
                {
                    Debug.WriteLine($"[GameSaveDetector] 文件名关键词匹配: {fileName} 包含 '{keyword}'");
                    return true;
                }
            }

            // 使用启发式关键字匹配
            if (MatchesHeuristicKeywords(directory))
            {
                Debug.WriteLine($"[GameSaveDetector] 目录启发式匹配: {directory}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameSaveDetector] 检查文件是否为存档时出错: {filePath}, 错误: {ex.Message}");
            return false;
        }
    }

    private bool MatchesHeuristicKeywords(string path)
    {
        if (Galgame == null) return false;

        // 使用新的变体生成系统
        var allVariants = GenerateAllVariants(Galgame);
        var pathLower = path.ToLowerInvariant();

        Debug.WriteLine($"[GameSaveDetector] 检查路径启发式匹配: {path}");
        Debug.WriteLine($"[GameSaveDetector] 使用 {allVariants.Count} 个变体进行匹配");

        foreach (var variant in allVariants)
        {
            if (!string.IsNullOrEmpty(variant) && pathLower.Contains(variant.ToLowerInvariant()))
            {
                Debug.WriteLine($"[GameSaveDetector] 启发式关键词匹配: 变体 '{variant}' 在路径 {path} 中找到");
                return true;
            }
        }

        return false;
    }

    private bool ShouldStopEarly(List<string> detectedPaths, int fileThreshold, int confidenceThreshold)
    {
        if (detectedPaths.Count < fileThreshold)
        {
            Debug.WriteLine($"[GameSaveDetector] 未达到早停文件阈值: {detectedPaths.Count} < {fileThreshold}");
            return false;
        }

        // 统计每个目录的文件数量
        var directoryCounts = new Dictionary<string, int>();
        foreach (var path in detectedPaths)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                var normalizedDir = directory.ToLowerInvariant().TrimEnd('\\', '/');
                if (directoryCounts.ContainsKey(normalizedDir))
                    directoryCounts[normalizedDir]++;
                else
                    directoryCounts[normalizedDir] = 1;
            }
        }

        // 记录目录统计信息
        Debug.WriteLine($"[GameSaveDetector] 目录文件统计: {string.Join(", ", directoryCounts.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");

        // 如果有目录包含足够的文件，可以早停
        var shouldStop = directoryCounts.Any(kvp => kvp.Value >= confidenceThreshold);
        if (shouldStop)
        {
            var topDir = directoryCounts.OrderByDescending(kvp => kvp.Value).First();
            Debug.WriteLine($"[GameSaveDetector] 达到早停条件，目录 '{topDir.Key}' 包含 {topDir.Value} 个文件 (阈值: {confidenceThreshold})");
        }

        return shouldStop;
    }

    private List<string> FilterDetectedPaths()
    {
        var filteredPaths = new List<string>();

        lock (DetectedSavePaths)
        {
            // 去重和排序
            var uniquePaths = DetectedSavePaths.Distinct().ToList();
            Debug.WriteLine($"[GameSaveDetector] 过滤前路径数量: {DetectedSavePaths.Count}, 去重后: {uniquePaths.Count}");

            // 按文件大小和修改时间排序（最新的和较大的文件更可能是存档）
            var fileInfoList = uniquePaths.Select(path => new
            {
                Path = path,
                Info = new FileInfo(path),
                Score = CalculateSaveFileScore(path)
            })
            .OrderByDescending(x => x.Score)
            .Take(10) // 只返回前10个最可能的路径
            .ToList();

            Debug.WriteLine($"[GameSaveDetector] 过滤后保留前10个最高评分路径:");
            foreach (var file in fileInfoList)
            {
                Debug.WriteLine($"[GameSaveDetector] - {file.Path} (评分: {file.Score:F1})");
            }

            filteredPaths.AddRange(fileInfoList.Select(x => x.Path));
        }

        return filteredPaths;
    }

    private string FindBestSaveDirectory(List<string> paths)
    {
        if (paths.Count == 0) return string.Empty;

        Debug.WriteLine($"[GameSaveDetector] 开始分析 {paths.Count} 个路径以找到最佳存档目录");

        // 获取所有变体用于评分
        var allVariants = GenerateAllVariants(Galgame!);
        Debug.WriteLine($"[GameSaveDetector] 使用 {allVariants.Count} 个变体进行目录评分");

        // 计算每个目录的综合评分
        var directoryScores = new Dictionary<string, DirectoryScoreInfo>();

        foreach (var path in paths)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) continue;

            var normalizedDir = directory.ToLowerInvariant().TrimEnd('\\', '/');

            if (!directoryScores.ContainsKey(normalizedDir))
                directoryScores[normalizedDir] = new DirectoryScoreInfo { Directory = directory };

            var scoreInfo = directoryScores[normalizedDir];

            // 基础分数：文件数量
            scoreInfo.TotalScore += 10;
            scoreInfo.FileCount++;

            // 额外分数：文件质量评分
            var fileScore = CalculateSaveFileScore(path);
            scoreInfo.TotalScore += fileScore * 0.1;

            // 额外分数：变体匹配评分（使用更精确的匹配）
            var variantScore = CalculateVariantMatchScore(directory, allVariants);
            scoreInfo.TotalScore += variantScore;
            if (variantScore > 0)
            {
                scoreInfo.MatchedVariants.AddRange(GetMatchedVariants(directory, allVariants));
            }

            // 额外分数：路径深度（更具体的路径更可能是存档目录）
            var depth = directory.Count(c => c == '\\' || c == '/');
            if (depth >= 3 && depth <= 6)
                scoreInfo.TotalScore += 15 * (depth / 6.0);
            scoreInfo.Depth = depth;

            // 路径结构加分
            scoreInfo.TotalScore += CalculatePathStructureScore(directory);
        }

        if (directoryScores.Count == 0) return string.Empty;

        Debug.WriteLine($"[GameSaveDetector] 目录详细评分结果:");
        foreach (var kvp in directoryScores.OrderByDescending(kvp => kvp.Value.TotalScore))
        {
            var info = kvp.Value;
            Debug.WriteLine($"[GameSaveDetector] - {info.Directory}");
            Debug.WriteLine($"[GameSaveDetector]   总评分: {info.TotalScore:F1}");
            Debug.WriteLine($"[GameSaveDetector]   文件数: {info.FileCount}");
            Debug.WriteLine($"[GameSaveDetector]   路径深度: {info.Depth}");
            Debug.WriteLine($"[GameSaveDetector]   匹配变体: {string.Join(", ", info.MatchedVariants)}");
        }

        // 按评分排序，获取最佳目录
        var bestDirectoryEntry = directoryScores.OrderByDescending(kvp => kvp.Value.TotalScore).First();
        var bestDirectory = bestDirectoryEntry.Value.Directory;
        var bestScore = bestDirectoryEntry.Value.TotalScore;

        Debug.WriteLine($"[GameSaveDetector] 最佳目录: {bestDirectory} (评分: {bestScore:F1})");

        // 如果最佳目录评分足够高，直接返回
        if (bestScore >= 25)
        {
            Debug.WriteLine("[GameSaveDetector] 最佳目录评分足够高，直接返回");
            return bestDirectory;
        }

        Debug.WriteLine("[GameSaveDetector] 最佳目录评分不足，但仍返回最高评分目录");
        return bestDirectory;
    }

    /// <summary>
    /// 计算路径与变体的匹配评分
    /// </summary>
    private double CalculateVariantMatchScore(string directory, List<string> variants)
    {
        if (string.IsNullOrEmpty(directory) || variants == null || variants.Count == 0)
            return 0;

        var directoryLower = directory.ToLowerInvariant();
        var totalScore = 0.0;
        var matchedVariants = 0;

        foreach (var variant in variants)
        {
            if (string.IsNullOrEmpty(variant)) continue;

            var variantLower = variant.ToLowerInvariant();

            // 完全匹配（最高分）
            if (directoryLower.Contains(variantLower))
            {
                // 根据匹配的精确度给分
                if (directoryLower.EndsWith(variantLower))
                {
                    totalScore += 30; // 目录名以变体结尾，最高分
                }
                else if (directoryLower.Contains($"\\{variantLower}\\") || directoryLower.Contains($"/{variantLower}/"))
                {
                    totalScore += 25; // 变体作为独立目录名
                }
                else
                {
                    totalScore += 15; // 部分匹配
                }
                matchedVariants++;
            }
        }

        // 匹配的变体越多，额外加分
        if (matchedVariants > 0)
        {
            totalScore += matchedVariants * 5; // 每匹配一个变体额外加5分
            Debug.WriteLine($"[GameSaveDetector] 路径 '{directory}' 匹配到 {matchedVariants} 个变体，评分: {totalScore}");
        }

        return totalScore;
    }

    /// <summary>
    /// 获取匹配的变体列表
    /// </summary>
    private List<string> GetMatchedVariants(string directory, List<string> variants)
    {
        var matched = new List<string>();
        if (string.IsNullOrEmpty(directory) || variants == null) return matched;

        var directoryLower = directory.ToLowerInvariant();
        foreach (var variant in variants)
        {
            if (!string.IsNullOrEmpty(variant) && directoryLower.Contains(variant.ToLowerInvariant()))
            {
                matched.Add(variant);
            }
        }

        return matched;
    }

    /// <summary>
    /// 计算路径结构评分
    /// </summary>
    private double CalculatePathStructureScore(string directory)
    {
        var score = 0.0;
        var dirLower = directory.ToLowerInvariant();

        // 常见的存档目录模式加分
        var savePatterns = new[]
        {
            "save", "saves", "savedata", "save_data", "userdata", "user_data",
            "data", "game", "games", "appdata", "local", "roaming"
        };

        foreach (var pattern in savePatterns)
        {
            if (dirLower.Contains(pattern))
            {
                score += 8;
            }
        }

        // 特殊路径加分
        if (dirLower.Contains("appdata\\roaming"))
            score += 15;
        if (dirLower.Contains("appdata\\local"))
            score += 12;
        if (dirLower.Contains("my games"))
            score += 10;
        if (dirLower.Contains("saved games"))
            score += 10;

        return score;
    }

    /// <summary>
    /// 目录评分信息
    /// </summary>
    private class DirectoryScoreInfo
    {
        public string Directory { get; set; } = string.Empty;
        public double TotalScore { get; set; }
        public int FileCount { get; set; }
        public int Depth { get; set; }
        public List<string> MatchedVariants { get; set; } = new();
    }

    private double CalculateSaveFileScore(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var score = 0.0;

            // 文件大小适中的文件更可能是存档（1KB - 10MB）
            if (fileInfo.Length > 1024 && fileInfo.Length < 10 * 1024 * 1024)
                score += 30;
            else if (fileInfo.Length > 100 && fileInfo.Length < 100 * 1024 * 1024)
                score += 20;

            // 最近修改的文件更可能是存档
            var timeDiff = DateTime.Now - fileInfo.LastWriteTime;
            if (timeDiff.TotalMinutes < 10)
                score += 40;
            else if (timeDiff.TotalHours < 1)
                score += 30;
            else if (timeDiff.TotalDays < 1)
                score += 20;

            // 路径匹配启发式关键字
            if (MatchesHeuristicKeywords(filePath))
                score += 30;

            return score;
        }
        catch
        {
            return 0;
        }
    }

    public override string Title => "GameSaveDetectorTask_Title".GetLocalized();
}