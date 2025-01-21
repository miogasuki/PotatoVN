using Windows.Foundation.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Helpers;
using Newtonsoft.Json;

namespace GalgameManager.Models;

public partial class Category : ObservableObject
{
    public event Action? OnGalgamesChanged;
    public string Name { get; set; }= string.Empty;
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<Galgame> GalgamesX { get; }= new();
    [ObservableProperty] private string _imagePath = string.Empty;
    [ObservableProperty] private DateTime _lastPlayed = DateTime.MinValue; // 所有游戏中最后一次玩的时间
    [ObservableProperty] private DateTime _lastClicked = DateTime.MinValue; // 上次点击该分类的时间

    public bool Belong(Galgame galgame) => GalgamesX.Contains(galgame);

    public Category()
    {
        
    }

    public Category(string name)
    {
        Name = name;
    }

    public string DisplayCount()
    {
        return "×" + GalgamesX.Count;
    }

    public void Add(Galgame galgame)
    {
        if (GalgamesX.Contains(galgame)) return;
        GalgamesX.Add(galgame);
        if (!galgame.Categories.Contains(this)) galgame.Categories.Add(this);
        OnGalgamesChanged?.Invoke();
    }

    public void Add(Category category)
    {
        if (category == this) return;
        category.GalgamesX.ForEach(Add);
        OnGalgamesChanged?.Invoke();
    }
    
    public void Remove(Galgame galgame)
    {
        if (!GalgamesX.Contains(galgame)) return;
        GalgamesX.Remove(galgame);
        galgame.Categories.Remove(this);
        OnGalgamesChanged?.Invoke();
    }

    public void Delete()
    {
        GalgamesX.ForEach(g => g.Categories.Remove(this));
    }

    public override string ToString()
    {
        return Name;
    }
    
    public bool ApplySearchKey(string searchKey)
    {
        return Name.ContainX(searchKey);
    }
    
    public void UpdateLastPlayed()
    {
        if (GalgamesX.Count == 0) return;
        LastPlayed = GalgamesX.Max(g => g.LastPlayTime);
    }

    public Category ShallowClone() => (Category)MemberwiseClone();
}