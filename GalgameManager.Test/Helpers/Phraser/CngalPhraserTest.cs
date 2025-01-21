using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class CngalPhraserTest
{
    private CngalPhraser _phraser = new();

    [SetUp]
    public void Init()
    {
        _phraser = new();
    }

    [Test]
    [TestCase("三色绘恋")]
    public async Task PhraseTest(string name)
    {
        Galgame? game = new(name);
        game = await _phraser.GetGalgameInfo(game);
        if(game == null)
        {
            Assert.Fail();
            return;
        }
        
        if(game.Name != "三色绘恋S") Assert.Fail();
        if(game.Id != "81") Assert.Fail();
        if(game.Developer != "绘恋制作组") Assert.Fail();
        if(game.ReleaseDate != new DateTime(2020, 5, 20)) Assert.Fail();
        if(game.Characters.Count != 6) Assert.Fail();
        Assert.Pass();
    }
}