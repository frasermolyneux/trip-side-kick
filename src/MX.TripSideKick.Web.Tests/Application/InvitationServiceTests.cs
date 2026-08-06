using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Invitations;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Common;
using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using Moq;

using NodaTime;

namespace MX.TripSideKick.Web.Tests.Application;

public sealed class InvitationServiceTests
{
    private readonly Mock<IInvitationRepository> invitationRepository = new();
    private readonly Mock<IMembershipRepository> membershipRepository = new();
    private readonly Mock<ITravellerRepository> travellerRepository = new();
    private readonly Mock<IInvitationNotifier> notifier = new();
    private readonly FixedClock clock = new(Instant.FromUtc(2025, 1, 1, 0, 0));
    private readonly InvitationService sut;
    private readonly TripId tripId = TripId.New();

    public InvitationServiceTests()
    {
        var membershipAccess = new MembershipAccessService(membershipRepository.Object);
        sut = new InvitationService(
            invitationRepository.Object, membershipRepository.Object, travellerRepository.Object,
            membershipAccess, notifier.Object, new PassthroughUnitOfWork(), clock);
    }

    [Fact]
    public async Task CreateInvitationAsync_requires_the_owner_role()
    {
        var editor = Membership.Create(tripId, "editor-subject", MembershipRole.Editor);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "editor-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CreateInvitationAsync(tripId, "editor-subject", new CreateInvitationInput("friend@example.com", MembershipRole.Viewer)));
    }

    [Fact]
    public async Task CreateInvitationAsync_notifies_and_persists_a_pending_invitation()
    {
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        invitationRepository.Setup(r => r.AddAsync(It.IsAny<Invitation>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        notifier.Setup(n => n.NotifyCreatedAsync(It.IsAny<Invitation>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var invitation = await sut.CreateInvitationAsync(
            tripId, "owner-subject", new CreateInvitationInput("friend@example.com", MembershipRole.Editor));

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal("friend@example.com", invitation.InvitedEmail);
        notifier.Verify(n => n.NotifyCreatedAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInvitationAsync_rejects_linking_an_already_linked_existing_traveller()
    {
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        var otherMembership = Membership.Create(tripId, "someone-else", MembershipRole.Viewer);
        var traveller = Traveller.Create(tripId, "Already linked", otherMembership.Id);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        travellerRepository.Setup(r => r.GetAsync(traveller.Id, It.IsAny<CancellationToken>())).ReturnsAsync(traveller);

        var input = new CreateInvitationInput(
            "friend@example.com", MembershipRole.Viewer, TravellerLinkKind.ExistingTraveller, traveller.Id);

        await Assert.ThrowsAsync<AlreadyMemberException>(() => sut.CreateInvitationAsync(tripId, "owner-subject", input));
    }

    [Fact]
    public async Task AcceptInvitationAsync_rejects_a_mismatched_verified_email()
    {
        var invitation = Invitation.Create(tripId, "friend@example.com", MembershipRole.Viewer, clock.GetCurrentInstant());
        invitationRepository
            .Setup(r => r.GetByAcceptanceTokenAsync(invitation.AcceptanceToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        await Assert.ThrowsAsync<InvitationIdentityMismatchException>(() =>
            sut.AcceptInvitationAsync(invitation.AcceptanceToken, "new-subject", "someone-else@example.com"));
    }

    [Fact]
    public async Task AcceptInvitationAsync_rejects_when_the_accepting_subject_is_already_a_member()
    {
        var invitation = Invitation.Create(tripId, "friend@example.com", MembershipRole.Viewer, clock.GetCurrentInstant());
        var existingMembership = Membership.Create(tripId, "new-subject", MembershipRole.Viewer);
        invitationRepository
            .Setup(r => r.GetByAcceptanceTokenAsync(invitation.AcceptanceToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "new-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMembership);

        await Assert.ThrowsAsync<AlreadyMemberException>(() =>
            sut.AcceptInvitationAsync(invitation.AcceptanceToken, "new-subject", "friend@example.com"));
    }

    [Fact]
    public async Task AcceptInvitationAsync_creates_a_membership_and_marks_the_invitation_accepted_for_a_non_travelling_planner()
    {
        var invitation = Invitation.Create(tripId, "friend@example.com", MembershipRole.Editor, clock.GetCurrentInstant());
        invitationRepository
            .Setup(r => r.GetByAcceptanceTokenAsync(invitation.AcceptanceToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "new-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);
        membershipRepository.Setup(r => r.AddAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        invitationRepository
            .Setup(r => r.UpdateAsync(invitation, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var membership = await sut.AcceptInvitationAsync(invitation.AcceptanceToken, "new-subject", "friend@example.com");

        Assert.Equal(MembershipRole.Editor, membership.Role);
        Assert.Equal("new-subject", membership.SubjectId);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        travellerRepository.Verify(r => r.AddAsync(It.IsAny<Traveller>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptInvitationAsync_links_the_intended_existing_traveller_without_creating_a_duplicate()
    {
        var traveller = Traveller.Create(tripId, "Sam", linkedMembershipId: null);
        var invitation = Invitation.Create(
            tripId, "friend@example.com", MembershipRole.Editor, clock.GetCurrentInstant(),
            TravellerLinkKind.ExistingTraveller, traveller.Id);
        invitationRepository
            .Setup(r => r.GetByAcceptanceTokenAsync(invitation.AcceptanceToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "new-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);
        membershipRepository.Setup(r => r.AddAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        travellerRepository.Setup(r => r.GetAsync(traveller.Id, It.IsAny<CancellationToken>())).ReturnsAsync(traveller);
        travellerRepository
            .Setup(r => r.UpdateAsync(traveller, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        invitationRepository
            .Setup(r => r.UpdateAsync(invitation, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var membership = await sut.AcceptInvitationAsync(invitation.AcceptanceToken, "new-subject", "friend@example.com");

        Assert.Equal(membership.Id, traveller.LinkedMembershipId);
        travellerRepository.Verify(r => r.AddAsync(It.IsAny<Traveller>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptInvitationAsync_creates_a_new_linked_traveller_when_requested()
    {
        var invitation = Invitation.Create(
            tripId, "friend@example.com", MembershipRole.Editor, clock.GetCurrentInstant(),
            TravellerLinkKind.NewLinkedTraveller, newTravellerDisplayName: "New Traveller");
        invitationRepository
            .Setup(r => r.GetByAcceptanceTokenAsync(invitation.AcceptanceToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "new-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);
        membershipRepository.Setup(r => r.AddAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Traveller? capturedTraveller = null;
        travellerRepository
            .Setup(r => r.AddAsync(It.IsAny<Traveller>(), It.IsAny<CancellationToken>()))
            .Callback<Traveller, CancellationToken>((t, _) => capturedTraveller = t)
            .Returns(Task.CompletedTask);
        invitationRepository
            .Setup(r => r.UpdateAsync(invitation, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var membership = await sut.AcceptInvitationAsync(invitation.AcceptanceToken, "new-subject", "friend@example.com");

        Assert.NotNull(capturedTraveller);
        Assert.Equal("New Traveller", capturedTraveller!.DisplayName);
        Assert.Equal(membership.Id, capturedTraveller.LinkedMembershipId);
    }

    [Fact]
    public async Task ResendInvitationAsync_notifies_and_generates_a_new_token()
    {
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        var invitation = Invitation.Create(tripId, "friend@example.com", MembershipRole.Viewer, clock.GetCurrentInstant());
        var originalToken = invitation.AcceptanceToken;
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        invitationRepository.Setup(r => r.GetAsync(invitation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        invitationRepository
            .Setup(r => r.UpdateAsync(invitation, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notifier.Setup(n => n.NotifyResentAsync(invitation, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.ResendInvitationAsync(tripId, invitation.Id, "owner-subject");

        Assert.NotEqual(originalToken, invitation.AcceptanceToken);
        notifier.Verify(n => n.NotifyResentAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeInvitationAsync_requires_the_owner_role()
    {
        var editor = Membership.Create(tripId, "editor-subject", MembershipRole.Editor);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "editor-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.RevokeInvitationAsync(tripId, InvitationId.New(), "editor-subject"));
    }
}
