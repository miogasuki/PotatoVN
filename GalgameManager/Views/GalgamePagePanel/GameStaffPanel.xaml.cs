using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.GalgamePagePanel;

public partial class GameStaffPanel
{
    public ObservableCollection<StaffRelation> Staffs = [];
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    private readonly IStaffService _staffService = App.GetService<IStaffService>();

    public GameStaffPanel()
    {
        InitializeComponent();
    }

    protected override void Update()
    {
        if (Panel is null || Game is null) return;
        Staffs.SyncCollection(_staffService.GetStaffs(Game!).Select(staff => new StaffRelation()
        {
            Staff = staff,
            Relation = string.Join(", ",
                staff.GetRelation(Game)?.Select(career => $"Career_Relation_{career}".GetLocalized()) ?? []),
            RelationEnum = staff.GetRelation(Game)?.Min() ?? Career.Unknown,
        }).ToList());
        Staffs.Sort((a, b) => a.RelationEnum.CompareTo(b.RelationEnum));
        Panel.Visibility = Staffs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StaffRelation staff) return;
        _navigationService.NavigateTo(typeof(StaffViewModel).FullName!,
            new StaffViewModel.StaffPageNavigationParameter { Staff = staff.Staff });
    }
}

public class StaffRelation
{
    public Staff Staff { get; set; } = null!;
    public string Relation { get; set; } = string.Empty;
    public Career RelationEnum; // 用于排序
}