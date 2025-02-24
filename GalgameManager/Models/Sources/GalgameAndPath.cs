namespace GalgameManager.Models.Sources;

public class GalgameAndPath(Galgame game, string path)
{
    public Galgame Galgame { get; set; } = game;
    public string Path { get; set; } = path;
}

public class GalgameAndPathDbDto
{
    public Guid GalgameId { get; set; }
    public string Path { get; set; } = string.Empty;
    
    public GalgameAndPathDbDto(Guid id, string path)
    {
        GalgameId = id;
        Path = path;
    }

    public GalgameAndPathDbDto() { }    
}