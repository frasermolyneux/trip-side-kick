using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Abstractions;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;
using MX.TripSideKick.Web.Api;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// Journey 2's role/membership management: only Owners invite/remove/change roles; Editors manage
/// content, not membership; signed-in Viewers cannot mutate anything; the last Owner can never
/// leave, be removed, or be demoted. App hosts only.
/// </summary>
[ApiController]
[Authorize]
[Route("v1/trips/{tripId:guid}/members")]
[Produces("application/json")]
public sealed class MembersController(MembershipService membershipService, ICurrentUser currentUser) : ControllerBase
{
    private readonly MembershipService membershipService = membershipService
        ?? throw new ArgumentNullException(nameof(membershipService));
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

    /// <summary>Lists members. Any member (including Viewers) may see who else is on the trip.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MembershipResponse>>> List(Guid tripId, CancellationToken cancellationToken)
    {
        var members = await membershipService
            .ListMembersAsync(new TripId(tripId), RequireSubjectId(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(members.Select(MembershipResponse.From).ToList());
    }

    /// <summary>Changes a member's role. Owner-only. Blocked if it would demote the last Owner.</summary>
    [HttpPut("{membershipId:guid}/role")]
    public async Task<ActionResult<MembershipResponse>> ChangeRole(
        Guid tripId, Guid membershipId, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!RowVersionETag.TryRequireIfMatch(Request, out var expectedRowVersion, out var failure))
        {
            return failure!;
        }

        var membership = await membershipService.ChangeRoleAsync(
            new TripId(tripId),
            new MembershipId(membershipId),
            request.Role,
            RequireSubjectId(),
            expectedRowVersion,
            cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(membership.RowVersion);
        return Ok(MembershipResponse.From(membership));
    }

    /// <summary>Removes a member from the trip. Owner-only. Blocked if the target is the last Owner.</summary>
    [HttpDelete("{membershipId:guid}")]
    public async Task<IActionResult> Remove(Guid tripId, Guid membershipId, CancellationToken cancellationToken)
    {
        await membershipService
            .RemoveMemberAsync(new TripId(tripId), new MembershipId(membershipId), RequireSubjectId(), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>The signed-in member leaves the trip. Blocked if they are the last Owner.</summary>
    [HttpPost("leave")]
    public async Task<IActionResult> Leave(Guid tripId, CancellationToken cancellationToken)
    {
        await membershipService.LeaveTripAsync(new TripId(tripId), RequireSubjectId(), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private string RequireSubjectId() =>
        currentUser.SubjectId ?? throw new InvalidOperationException("An authenticated request must have a subject id.");
}

/// <summary>Request body for <c>PUT /v1/trips/{tripId}/members/{membershipId}/role</c>.</summary>
public sealed record ChangeRoleRequest(MembershipRole Role);

/// <summary>Response contract for a membership.</summary>
public sealed record MembershipResponse(Guid Id, Guid TripId, string SubjectId, MembershipRole Role, string ETag)
{
    public static MembershipResponse From(Membership membership) => new(
        membership.Id.Value,
        membership.TripId.Value,
        membership.SubjectId,
        membership.Role,
        RowVersionETag.ToETag(membership.RowVersion));
}
