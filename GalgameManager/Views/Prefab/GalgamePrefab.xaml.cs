using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using DependencyPropertyGenerator;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Views.Prefab;

[DependencyProperty<Stretch>("ImageStretch", DefaultValue = Stretch.UniformToFill,
    DefaultBindingMode = DefaultBindingMode.OneWay)]
[DependencyProperty<Galgame>("Galgame")]
[DependencyProperty<Visibility>("PlayTypeVisibility", DefaultValue = Visibility.Collapsed,
    DefaultBindingMode = DefaultBindingMode.OneWay)]
[DependencyProperty<Visibility>("SourceVisibility", DefaultValue = Visibility.Collapsed,
    DefaultBindingMode = DefaultBindingMode.OneWay)]
[DependencyProperty<FlyoutBase>("Flyout")]
[DependencyProperty<double>("ItemScale", DefaultValue = 1.0f)]
[DependencyProperty<double>("TextHeight", DefaultValue = 80f)]
public sealed partial class GalgamePrefab
{
    public double MediumFontSize = 10f;
    
    public GalgamePrefab()
    {
        if (Application.Current.Resources["MediumFontSize"] is double mediumFontSize)
            MediumFontSize = mediumFontSize;
        InitializeComponent();
    }
    
    partial void OnItemScaleChanged(double newValue)
    {
        if (newValue > 0) return;
        ItemScale = 1.0f;
    }
    
    public double CalcValue(double value) => value * ItemScale;
}