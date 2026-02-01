using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
[Category("Phraser")]
public class VndbPhraserTest
{
    private VndbPhraser _vndbPhraser = null!;

    [SetUp]
    public void Init()
    {
        _vndbPhraser = new VndbPhraser();
    }

    [Test]
    [TestCase("スタディ§ステディ", "24689", "スタディ§ステディ", null, new string[] { })]
    [TestCase("サノバウィッチ", "16044", null, null, new string[] { })]
    [TestCase("喫茶ステラと死神の蝶", "26414", null, "星光咖啡馆与死神之蝶", new[] { "明月 栞那" })]
    // 特例：Description为空
    [TestCase("妹調教日記～こんなツンデレが俺の妹なわけない!～", "9303", null, "妹调教日记", new string[] { })]
    public async Task ParseGameWithNameTest(
        string inputGameName,
        string expectedId,
        string? expectedName,
        string? expectedCnName,
        string[] expectedCharacterNames)
    {
        Galgame? game = new(inputGameName);
        game = await _vndbPhraser.GetGalgameInfo(game);
        ParserTestUtil.CheckGame(game, expectedId, expectedName: expectedName, expectedCnName: expectedCnName,
            characterPhraser: _vndbPhraser, expectedCharacterNames: expectedCharacterNames);
    }

    [Test]
    [TestCase("八日 なのか", "s4808", null)]
    [TestCase("Amamiya Ritsu", "s2883", "Amamiya Ritsu is a Japanese")]
    public async Task ParseStaffWithNameTest(string name, string expectedId, string? expectedDescription)
    {
        Staff? staff = new() { JapaneseName = name };
        staff = await _vndbPhraser.GetStaffAsync(staff);
        ParserTestUtil.CheckStaff(staff, RssType.Vndb, expectedId, expectedDescription);
    }

    [Test]
    [TestCase("s4883", "冬壱 もんめ", "Fuyuichi Monme", null)]
    public async Task ParseStaffWithIdTest(string id, string? expectedJapaneseName, string? expectedEnglishName,
        string? expectedDescription)
    {
        Staff? staff = new() { Ids = { [(int)RssType.Vndb] = id } };
        staff = await _vndbPhraser.GetStaffAsync(staff);
        ParserTestUtil.CheckStaff(staff, RssType.Vndb, id, expectedDescription,
            expectedJapaneseName: expectedJapaneseName,
            expectedEnglishName: expectedEnglishName);
    }

    [Test]
    [TestCase("紙の上の魔法使い", "High School Student Heroine", "Brother/Sister Romance")]
    [TestCase("Gyakuten Saiban", "Mystery", "Falsely Accused")]
    public async Task ParseGameWithIdandEnglishTest(string name, string expectedTag1, string expectedTag2)
    {
        // 没有Token，不是中文环境
        VndbPhraserData data = new(null, false);
        _vndbPhraser.UpdateData(data);

        Galgame? game = new(name);
        game = await _vndbPhraser.GetGalgameInfo(game);


        Assert.That(game, Is.Not.Null);
        Assert.That(game.Tags.Value, Has.Member(expectedTag1));
        Assert.That(game.Tags.Value, Has.Member(expectedTag2));
    }

    [Test]
    [TestCase("紙の上の魔法使い", "傲娇女主角", "主人公的妹妹女主角")]
    [TestCase("Gyakuten Saiban", "智斗", "被冤枉")]
    public async Task ParseGameWithIdandChineseTest(string name, string expectedTag1, string expectedTag2)
    {
        // 没有Token，不是中文环境
        VndbPhraserData data = new(null, true);
        _vndbPhraser.UpdateData(data);

        Galgame? game = new(name);
        game = await _vndbPhraser.GetGalgameInfo(game);


        Assert.That(game, Is.Not.Null);
        Assert.That(game.Tags.Value, Has.Member(expectedTag1));
        Assert.That(game.Tags.Value, Has.Member(expectedTag2));
    }
}