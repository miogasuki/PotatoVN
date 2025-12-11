using System.Collections.ObjectModel;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Views.Dialog;

public sealed partial class ImagePickerDialog : ContentDialog
{
    public ObservableCollection<ImagePickerItem> Images { get; } = new();
    public string? SelectedImageUrl { get; private set; }

    private readonly bool _isHeader;
    private Button? _selectedButton;
    
    // UI Components
    private readonly ScrollViewer _rootScrollViewer;
    private readonly Grid _masonryGrid;
    
    // Layout State
    private readonly List<StackPanel> _columns = new();
    private readonly List<ImageWrapper> _imageWrappers = new();
    private double[] _columnRatios = Array.Empty<double>(); // Tracks sum of aspect ratios per column

    public ImagePickerDialog(IEnumerable<string> images, bool isHeader = false)
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        _isHeader = isHeader;

        Title = "选择图片";
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        DefaultButton = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;

        // Initialize UI Container
        _masonryGrid = new Grid
        {
            Padding = new Thickness(10),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Top 
        };

        _rootScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = _masonryGrid
        };

        this.Content = _rootScrollViewer;

        // Initialize Data and Start Loading
        foreach (var url in images.Where(img => !string.IsNullOrEmpty(img)))
        {
            var item = new ImagePickerItem { Url = url };
            Images.Add(item);
            _imageWrappers.Add(new ImageWrapper(item, OnImageLoaded, OnImageClick));
        }
        
        this.Loaded += ImagePickerDialog_Loaded;
        this.Unloaded += ImagePickerDialog_Unloaded;
    }

    private void ImagePickerDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot != null)
        {
            XamlRoot.Changed += XamlRoot_Changed;
            RecalculateLayout(XamlRoot.Size);
        }
    }

    private void ImagePickerDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot != null)
        {
            XamlRoot.Changed -= XamlRoot_Changed;
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        RecalculateLayout(sender.Size);
    }

    private void RecalculateLayout(Size windowSize)
    {
        if (_imageWrappers.Count == 0) return;

        double maxDialogWidth = windowSize.Width * 0.9;
        double maxDialogHeight = windowSize.Height * 0.85;

        // Set limits
        _masonryGrid.Width = maxDialogWidth;
        _rootScrollViewer.MaxHeight = maxDialogHeight;

        // Determine column count
        double targetItemWidth = _isHeader ? 250 : 180;
        double availableWidth = maxDialogWidth - 40; // Approx padding
        int columnCount = (int)(availableWidth / targetItemWidth);
        columnCount = Math.Max(3, columnCount); // At least 3 columns

        if (_columns.Count == columnCount) return;

        // Rebuild Grid Structure
        _masonryGrid.Children.Clear();
        _masonryGrid.ColumnDefinitions.Clear();
        _columns.Clear();
        _columnRatios = new double[columnCount];

        for (int i = 0; i < columnCount; i++)
        {
            double columnWidth = (maxDialogWidth - 40 - (columnCount - 1) * 10) / columnCount;
            _masonryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWidth) });

            var stackPanel = new StackPanel { Spacing = 10 };
            Grid.SetColumn(stackPanel, i);
            _masonryGrid.Children.Add(stackPanel);
            _columns.Add(stackPanel);
        }

        // Re-distribute already loaded images
        foreach (var wrapper in _imageWrappers)
        {
            if (wrapper.Button.Parent is StackPanel oldParent)
            {
                oldParent.Children.Remove(wrapper.Button);
            }

            // Always add wrapper, even if not fully loaded, to show placeholders
            AddToShortestColumn(wrapper);
        }
    }

    private void AddToShortestColumn(ImageWrapper wrapper)
    {
        if (_columns.Count == 0) return;

        int minIndex = 0;
        double minRatio = _columnRatios[0];

        for (int i = 1; i < _columns.Count; i++)
        {
            if (_columnRatios[i] < minRatio)
            {
                minRatio = _columnRatios[i];
                minIndex = i;
            }
        }

        _columns[minIndex].Children.Add(wrapper.Button);
        _columnRatios[minIndex] += wrapper.AspectRatio;
    }

    private void OnImageLoaded(ImageWrapper wrapper)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            PlaceImage();
        }
        else
        {
            DispatcherQueue.TryEnqueue(PlaceImage);
        }

        void PlaceImage()
        {
            if (wrapper.Button.Parent != null) return;
            if (_columns.Count == 0) return;

            // Calculate and set image height based on aspect ratio
            if (_masonryGrid.ColumnDefinitions.Count > 0 && wrapper.AspectRatio > 0)
            {
                double columnWidth = _masonryGrid.ColumnDefinitions[0].ActualWidth;
                if (columnWidth > 0)
                {
                    double imageHeight = columnWidth / wrapper.AspectRatio;
                    wrapper.Button.Height = Math.Max(100, imageHeight + 4); // Add border thickness
                }
            }

            AddToShortestColumn(wrapper);
        }
    }

    private void OnImageClick(ImageWrapper wrapper)
    {
        if (_selectedButton != null)
        {
            UpdateSelectionVisual(_selectedButton, false);
        }

        _selectedButton = wrapper.Button;
        SelectedImageUrl = wrapper.Item.Url;
        UpdateSelectionVisual(_selectedButton, true);
        
        IsPrimaryButtonEnabled = true;
    }

    private void UpdateSelectionVisual(Button button, bool isSelected)
    {
        if (button.Content is Border border)
        {
            if (isSelected)
            {
                border.BorderBrush = Application.Current.Resources["SystemControlHighlightAccentBrush"] as Brush;
                border.BorderThickness = new Thickness(4);
            }
            else
            {
                border.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                border.BorderThickness = new Thickness(2);
            }
        }
    }

    private class ImageWrapper
    {
        public ImagePickerItem Item { get; }
        public Button Button { get; }
        public bool IsLoaded { get; private set; }
        public double AspectRatio { get; private set; } = 1.0; 

        private readonly Action<ImageWrapper> _onLoaded;
        private readonly Action<ImageWrapper> _onClick;

        public ImageWrapper(ImagePickerItem item, Action<ImageWrapper> onLoaded, Action<ImageWrapper> onClick)
        {
            Item = item;
            _onLoaded = onLoaded;
            _onClick = onClick;

            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                // Opacity = 0 // Removed fade-in for now to ensure visibility
            };

            // Use a Border as the container with a placeholder background and MinHeight
            var border = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray) { Opacity = 0.3 }, // Placeholder
                MinHeight = 100, // Prevent collapse
                Child = image
            };

            Button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Content = border
            };
            Button.Click += (s, e) => _onClick(this);

            try
            {
                var bitmap = new BitmapImage();
                bitmap.ImageOpened += (s, e) =>
                {
                    if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                    {
                        AspectRatio = (double)bitmap.PixelWidth / bitmap.PixelHeight;
                    }
                    MarkLoaded();
                };
                bitmap.ImageFailed += (s, e) => MarkLoaded();

                if (Uri.TryCreate(item.Url, UriKind.RelativeOrAbsolute, out var uri))
                {
                    bitmap.UriSource = uri;
                    image.Source = bitmap;
                }
                else
                {
                    MarkLoaded(); // Fail gracefully
                }
            }
            catch
            {
                MarkLoaded();
            }

            void MarkLoaded()
            {
                IsLoaded = true;
                // Remove placeholder min-height/background if desired, 
                // but keeping them doesn't hurt (background is behind image).
                // Actually, if we want to remove MinHeight after load to fit tight:
                // border.MinHeight = 0; 
                // But let's keep it simple for stability.
                
                _onLoaded(this);
            }
        }
    }
}

public class ImagePickerItem : ObservableObject
{
    public string Url { get; set; } = string.Empty;
}
