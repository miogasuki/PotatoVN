using GalgameManager.Helpers.Phrase;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class MixedPhraserEnabledDialog
{
    public MixedPhraserEnabled Result { get; }

    public MixedPhraserEnabledDialog(MixedPhraserEnabled enabled)
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Result = new MixedPhraserEnabled
        {
            BangumiEnabled = enabled.BangumiEnabled,
            VndbEnabled = enabled.VndbEnabled,
            YmgalEnabled = enabled.YmgalEnabled,
            SteamEnabled = enabled.SteamEnabled
        };

        // 初始化 CheckBox 状态
        BangumiCheckBox.IsChecked = enabled.BangumiEnabled;
        VndbCheckBox.IsChecked = enabled.VndbEnabled;
        YmgalCheckBox.IsChecked = enabled.YmgalEnabled;
        SteamCheckBox.IsChecked = enabled.SteamEnabled;

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result.BangumiEnabled = BangumiCheckBox.IsChecked ?? false;
        Result.VndbEnabled = VndbCheckBox.IsChecked ?? false;
        Result.YmgalEnabled = YmgalCheckBox.IsChecked ?? false;
        Result.SteamEnabled = SteamCheckBox.IsChecked ?? false;
    }
}
