using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GalgameManager.Test.Upgrade;

internal static class UpgradeTestData
{
    private static string? _workspaceRoot;

    public static string WorkspaceRoot => _workspaceRoot ??= FindWorkspaceRoot();

    public static string GetFixtureDir(string version)
        => Path.Combine(WorkspaceRoot, "GalgameManager.Test", "TestData", version);

    public static void CopyJsonOnly(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }
    }

    public static int ReadExpectedGalgameCount(string fixtureDir)
    {
        var dataGalgamesPath = Path.Combine(fixtureDir, "data.galgames.json");
        if (File.Exists(dataGalgamesPath))
        {
            var arr = JArray.Parse(File.ReadAllText(dataGalgamesPath));
            return arr.Count;
        }

        var localSettingsPath = Path.Combine(fixtureDir, "LocalSettings.json");
        Assert.That(File.Exists(localSettingsPath), Is.True, $"未找到 LocalSettings.json: {localSettingsPath}");

        var obj = JObject.Parse(File.ReadAllText(localSettingsPath));
        var galgamesProp = obj.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, "galgames", StringComparison.OrdinalIgnoreCase));

        if (galgamesProp?.Value is JArray arr2) return arr2.Count;
        if (galgamesProp?.Value is null) return 0;

        // 兜底：若不是数组（理论上不该发生），尝试转换为数组
        return JArray.FromObject(galgamesProp.Value).Count;
    }

    /// <summary>
    /// 读取预期的 CategoryGroup 数量
    /// </summary>
    public static int ReadExpectedCategoryGroupCount(string fixtureDir)
    {
        // 1.8.0+ 格式: data.categoryGroups.json
        var dataCategoryGroupsPath = Path.Combine(fixtureDir, "data.categoryGroups.json");
        if (File.Exists(dataCategoryGroupsPath))
        {
            var arr = JArray.Parse(File.ReadAllText(dataCategoryGroupsPath));
            return arr.Count;
        }

        // 1.7.2 格式: LocalSettings.json 中的 categoryGroups
        var localSettingsPath = Path.Combine(fixtureDir, "LocalSettings.json");
        if (!File.Exists(localSettingsPath)) return 0;

        var obj = JObject.Parse(File.ReadAllText(localSettingsPath));
        var categoryGroupsProp = obj.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, "categoryGroups", StringComparison.OrdinalIgnoreCase));

        if (categoryGroupsProp?.Value is JArray arr2) return arr2.Count;
        return 0;
    }

    /// <summary>
    /// 读取预期的 Category 总数（所有 CategoryGroup 中的 Category 合计）
    /// </summary>
    public static int ReadExpectedCategoryCount(string fixtureDir)
    {
        // 1.8.0+ 格式: data.categoryGroups.json
        var dataCategoryGroupsPath = Path.Combine(fixtureDir, "data.categoryGroups.json");
        if (File.Exists(dataCategoryGroupsPath))
        {
            var arr = JArray.Parse(File.ReadAllText(dataCategoryGroupsPath));
            return arr.SelectMany(g => g["Categories"] ?? new JArray()).Count();
        }

        // 1.7.2 格式: LocalSettings.json 中的 categoryGroups
        var localSettingsPath = Path.Combine(fixtureDir, "LocalSettings.json");
        if (!File.Exists(localSettingsPath)) return 0;

        var obj = JObject.Parse(File.ReadAllText(localSettingsPath));
        var categoryGroupsProp = obj.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, "categoryGroups", StringComparison.OrdinalIgnoreCase));

        if (categoryGroupsProp?.Value is JArray arr2)
        {
            return arr2.SelectMany(g => g["Categories"] ?? new JArray()).Count();
        }
        return 0;
    }

    /// <summary>
    /// 读取预期的 GalgameSource 数量
    /// </summary>
    public static int ReadExpectedGalgameSourceCount(string fixtureDir)
    {
        // 1.8.0+ 格式: data.galgameSources.json
        var dataSourcesPath = Path.Combine(fixtureDir, "data.galgameSources.json");
        if (File.Exists(dataSourcesPath))
        {
            var arr = JArray.Parse(File.ReadAllText(dataSourcesPath));
            return arr.Count;
        }

        // 1.7.2 格式: LocalSettings.json 中的 galgameFolders（升级后变为 GalgameSource）
        var localSettingsPath = Path.Combine(fixtureDir, "LocalSettings.json");
        if (!File.Exists(localSettingsPath)) return 0;

        var obj = JObject.Parse(File.ReadAllText(localSettingsPath));
        var foldersProp = obj.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, "galgameFolders", StringComparison.OrdinalIgnoreCase));

        if (foldersProp?.Value is JArray arr2) return arr2.Count;
        return 0;
    }

    /// <summary>
    /// 读取夹具中“应被保留”的分类标识：优先用 Id（更稳），否则用 Name。
    /// </summary>
    public static IReadOnlyList<(Guid? Id, string Name)> ReadExpectedCategories(string fixtureDir)
    {
        var result = new List<(Guid? Id, string Name)>();

        // 1.8.0+ 格式: data.categoryGroups.json（Category/Group 都有 Id）
        var dataCategoryGroupsPath = Path.Combine(fixtureDir, "data.categoryGroups.json");
        if (File.Exists(dataCategoryGroupsPath))
        {
            var arr = JArray.Parse(File.ReadAllText(dataCategoryGroupsPath));
            foreach (var group in arr)
            {
                if (group is null) continue;
                foreach (var cat in group["Categories"] ?? new JArray())
                {
                    var name = cat?["Name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    Guid? id = null;
                    var idStr = cat?["Id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var parsed)) id = parsed;

                    result.Add((id, name));
                }
            }

            return Dedup(result);
        }

        // 1.7.2 格式: LocalSettings.json 中的 categoryGroups（Category 只有 Name）
        var localSettingsPath = Path.Combine(fixtureDir, "LocalSettings.json");
        if (!File.Exists(localSettingsPath)) return Array.Empty<(Guid?, string)>();

        var obj = JObject.Parse(File.ReadAllText(localSettingsPath));
        var categoryGroupsProp = obj.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, "categoryGroups", StringComparison.OrdinalIgnoreCase));

        if (categoryGroupsProp?.Value is JArray arr2)
        {
            foreach (var group in arr2)
            {
                foreach (var cat in group?["Categories"] ?? new JArray())
                {
                    var name = cat?["Name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    result.Add((null, name));
                }
            }
        }

        return Dedup(result);

        static IReadOnlyList<(Guid? Id, string Name)> Dedup(List<(Guid? Id, string Name)> items)
        {
            var seenId = new HashSet<Guid>();
            var seenName = new HashSet<string>(StringComparer.Ordinal);
            var deduped = new List<(Guid? Id, string Name)>();

            foreach (var (id, name) in items)
            {
                if (id is { } guid)
                {
                    if (seenId.Add(guid)) deduped.Add((guid, name));
                    continue;
                }

                if (seenName.Add(name)) deduped.Add((null, name));
            }

            return deduped;
        }
    }

    private static string FindWorkspaceRoot()
    {
        // 从测试输出目录向上找包含 GalgameManager.sln 的目录
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "GalgameManager.sln");
            if (File.Exists(candidate)) return dir.FullName;
        }

        Assert.Fail($"未找到工作区根目录（GalgameManager.sln）。当前测试目录: {AppContext.BaseDirectory}");
        return string.Empty;
    }
}
