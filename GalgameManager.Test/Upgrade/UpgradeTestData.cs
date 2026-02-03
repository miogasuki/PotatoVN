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
