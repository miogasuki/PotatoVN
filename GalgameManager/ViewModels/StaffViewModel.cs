using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;

namespace GalgameManager.ViewModels;

public partial class StaffViewModel(
    INavigationService navigationService, IFilterService filterService
) : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CareerString))] 
    private Staff? _staff;

    public string CareerString =>
        Staff?.Career.Count > 0 ? string.Join(", ", Staff.Career.Select(c => c.GetLocalized())) : "-";

    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not StaffPageNavigationParameter staffPageNavigationParameter)
        {
            Debug.Assert(false, "Invalid navigation parameter");
            return;
        }
        Staff = staffPageNavigationParameter.Staff;
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void NaviToGame(StaffGame? game)
    {
        if (game is null) return;
        NavigationHelper.NavigateToGalgamePage(navigationService, new GalgamePageParameter{Galgame = game.Game});
    }

    [RelayCommand]
    private void NaviToHome()
    {
        if (Staff is null) return;
        NavigationHelper.NavigateToHomePage(navigationService, filterService, [new StaffFilter(Staff)]);
    }
    
    public class StaffPageNavigationParameter
    {
        public required Staff Staff { get; set; }
    }
}