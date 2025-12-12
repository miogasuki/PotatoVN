using System.Collections.ObjectModel;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Views.Dialog;

public sealed partial class ImagePickerDialog : ContentDialog
{
    public ObservableCollection<ImagePickerItem> Images { get; } = new();
    public string? SelectedImageUrl { get; private set; }
    public double ItemWidth { get; set; }
    public double ItemHeight { get; set; }

    public ImagePickerDialog(IEnumerable<string> images, bool isHeader = false)
    {
        ItemWidth = isHeader ? 320 : 150;
        ItemHeight = isHeader ? 180 : 209;
        
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;

        Title = "选择图片";
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        DefaultButton = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;

        foreach (var url in images.Where(img => !string.IsNullOrEmpty(img)))
        {
            Images.Add(new ImagePickerItem { Url = url });
        }
    }

    private void ImageGridView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is GridView { SelectedItem: ImagePickerItem item })
        {
            SelectedImageUrl = item.Url;
            IsPrimaryButtonEnabled = true;
        }
        else
        {
            SelectedImageUrl = null;
            IsPrimaryButtonEnabled = false;
        }
    }
}

public class ImagePickerItem : ObservableObject
{
    public string Url { get; set; } = string.Empty;
}
