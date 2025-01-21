using System.Diagnostics;
using DependencyPropertyGenerator;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace GalgameManager.Views.Prefab;

[DependencyProperty<FlyoutBase>("Flyout")]
[DependencyProperty<GalgameSourceBase>("Source")]
public sealed partial class SourcePrefab
{
    public SourcePrefab()
    {
        InitializeComponent();
        Loaded += SourcePrefab_Loaded;
    }

    private void SourcePrefab_Loaded(object sender, RoutedEventArgs e)
    {
        Debug.Assert(Source != null, "Source property should not be null.");
    }
}

public class SourcePrefabTemplateSelector : DataTemplateSelector
{
    public DataTemplate ImageTemplate { get; set; } = null!;
    public DataTemplate FontIconTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is not GalgameSourceBase source) return base.SelectTemplateCore(item);
        return string.IsNullOrEmpty(source.ImagePath) ? FontIconTemplate : ImageTemplate;
    }
}
