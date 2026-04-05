using System.Windows.Controls;
using CateringEquipmentApp.Models;
using CateringEquipmentApp.Services;

namespace CateringEquipmentApp.Pages;

public partial class DashboardPage : Page
{
    private readonly DatabaseService _databaseService = new();
    private readonly UserSession _session;

    public DashboardPage(UserSession session)
    {
        InitializeComponent();
        _session = session;
        SessionInfoText.Text = $"{session.FullName}\n{session.RoleName} · {session.PositionName}";
    }

    public async Task RefreshAsync()
    {
        var stats = await _databaseService.GetDashboardStatsAsync();
        EquipmentCountText.Text = stats.EquipmentCount.ToString();
        OpenRequestsCountText.Text = stats.OpenRequestsCount.ToString();
        RepairsCountText.Text = stats.RepairsCount.ToString();
        ReplacementsCountText.Text = stats.ReplacementsCount.ToString();
        SanitationCountText.Text = stats.SanitationCount.ToString();
        AppealsCountText.Text = stats.AppealsCount.ToString();
    }
}
