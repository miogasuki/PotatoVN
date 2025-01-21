using DependencyPropertyGenerator;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Control
{
    [DependencyProperty<string>("Title")]
    [DependencyProperty<string>("Description")]
    public sealed partial class Setting
    {
        public Setting()
        {
            InitializeComponent();
        }
        
        public static readonly new DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content), typeof(UIElement), typeof(Setting),
            new PropertyMetadata(null, OnContentChanged));

        public new UIElement Content
        {
            get => (UIElement)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Setting setting)
            {
                setting.ContentArea.Content = e.NewValue;
            }
        }

        private void Setting_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DescriptionTextBlock.MaxWidth = ActualWidth - Content.ActualSize.X - 40;
        }
    }
}