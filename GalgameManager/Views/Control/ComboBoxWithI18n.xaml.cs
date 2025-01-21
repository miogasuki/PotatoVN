using DependencyPropertyGenerator;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control;

[DependencyProperty<DataTemplate>("ItemTemplate")]
[DependencyProperty<object>("ItemsSource")]
[DependencyProperty<object>("SelectedItem")]
public partial class ComboBoxWithI18N
{
    public ComboBoxWithI18N()
    {
        InitializeComponent();
        ComboBox.SelectionChanged += (_, _) => SelectedItem = ComboBox.SelectedItem;
    }

    partial void OnSelectedItemChanged(object? newValue)
    {
        if (newValue != ComboBox.SelectedItem)
            ComboBox.SelectedItem = newValue;
    }
}

public class ComboBoxWithI18NDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultTemplate { get; set; } = null!;
    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return ItemTemplate ?? DefaultTemplate;
    }
}