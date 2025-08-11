using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class BasicDialog
{
    public bool CheckBoxChecked { get; private set; }
    public bool PrimaryButtonClicked { get; private set; }
    
    public BasicDialog(string title, string message, string? primaryButton = null, string? cancelButton = null,
        string? checkBoxText = null)
    {
        InitializeComponent();
        Title = title;
        TextBlock.Text = message;
        PrimaryButtonText = primaryButton ?? "Yes".GetLocalized();
        CloseButtonText = cancelButton ?? "Cancel".GetLocalized();
        CheckBox.Visibility = checkBoxText is null ?  Visibility.Collapsed : Visibility.Visible;
        CheckBox.Content = checkBoxText;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        
        PrimaryButtonClick += (_, _) =>
        {
            CheckBoxChecked = CheckBox.IsChecked ?? false;
            PrimaryButtonClicked = true;
        };
    }
}