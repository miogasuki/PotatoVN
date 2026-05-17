using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class HomeFilterPanelVisibilityDialog
{
    public bool ShowPlayStatusAndSourcePanel { get; private set; }
    public bool ShowEnginePanel { get; private set; }
    public bool ShowDeveloperPanel { get; private set; }
    public bool ShowTagPanel { get; private set; }

    public HomeFilterPanelVisibilityDialog(bool showPlayStatusAndSourcePanel, bool showEnginePanel,
        bool showDeveloperPanel, bool showTagPanel)
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;

        ShowPlayStatusAndSourcePanel = showPlayStatusAndSourcePanel;
        ShowEnginePanel = showEnginePanel;
        ShowDeveloperPanel = showDeveloperPanel;
        ShowTagPanel = showTagPanel;

        PlayStatusAndSourcePanelCheckBox.IsChecked = showPlayStatusAndSourcePanel;
        EnginePanelCheckBox.IsChecked = showEnginePanel;
        DeveloperPanelCheckBox.IsChecked = showDeveloperPanel;
        TagPanelCheckBox.IsChecked = showTagPanel;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ShowPlayStatusAndSourcePanel = PlayStatusAndSourcePanelCheckBox.IsChecked ?? false;
        ShowEnginePanel = EnginePanelCheckBox.IsChecked ?? false;
        ShowDeveloperPanel = DeveloperPanelCheckBox.IsChecked ?? false;
        ShowTagPanel = TagPanelCheckBox.IsChecked ?? false;
    }
}
