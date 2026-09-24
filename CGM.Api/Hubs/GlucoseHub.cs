using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CGM.Api.Hubs;

[Authorize]
public class GlucoseHub : Hub
{
    // Family members join a group specific to their FamilyId
    public async Task JoinFamilyGroup(string familyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"family_{familyId}");
    }

    public async Task LeaveFamilyGroup(string familyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"family_{familyId}");
    }
}
