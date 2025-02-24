using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.GalgamePagePanel;

public partial class GameCharacterPanel
{
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();

    public GameCharacterPanel()
    {
        InitializeComponent();
    }

    protected override void Update()
    {
        if (Panel is null) return;
        Panel.Visibility = Game?.Characters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not GalgameCharacter character) return;
        _navigationService.NavigateTo(typeof(GalgameCharacterViewModel).FullName!,
            new GalgameCharacterParameter { GalgameCharacter = character });
    }
}