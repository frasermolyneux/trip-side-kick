using System.Data;
using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;
using MX.TripSideKick.Infrastructure.Persistence;
using MX.TripSideKick.Infrastructure.Persistence.Repositories;

namespace MX.TripSideKick.Web.Tests.Integration;

[Collection(SqlServerTestGroup.Name)]
public sealed class PostgresPersistenceIntegrationTests(SqlServerContainerFixture fixture)
{
    private TripSideKickDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<TripSideKickDbContext>().UseNpgsql(fixture.ConnectionString).Options);

    [Fact]
    public async Task Database_generated_ETags_change_and_stale_writes_are_rejected()
    {
        await using var first = CreateDatabase();
        var trip = Trip.Create("Postgres concurrency");
        first.Trips.Add(trip);
        await first.SaveChangesAsync();
        var original = trip.RowVersion!.ToArray();

        await using var second = CreateDatabase();
        var stale = await second.Trips.SingleAsync(item => item.Id == trip.Id);
        trip.Rename("Winner");
        await first.SaveChangesAsync();
        Assert.NotEqual(original, trip.RowVersion);
        Assert.Equal(16, trip.RowVersion!.Length);
        stale.Rename("Loser");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_membership_is_a_domain_conflict()
    {
        await using var database = CreateDatabase();
        var id = TripId.New();
        database.Memberships.Add(Membership.Create(id, "duplicate-postgres", MembershipRole.Owner));
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var repository = new SqlMembershipRepository(database);
        await Assert.ThrowsAsync<AlreadyMemberException>(() =>
            repository.AddAsync(Membership.Create(id, "duplicate-postgres", MembershipRole.Viewer)));
    }

    [Fact]
    public async Task Serializable_unit_of_work_uses_database_isolation_and_rolls_back_on_failure()
    {
        await using var database = CreateDatabase();
        var unit = new EfUnitOfWork(database);
        var trip = Trip.Create("Rollback");
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteSerializableAsync(async cancellationToken =>
        {
            Assert.Equal(IsolationLevel.Serializable, database.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            database.Trips.Add(trip);
            await database.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Rollback test");
        }));
        database.ChangeTracker.Clear();
        Assert.False(await database.Trips.AnyAsync(item => item.Id == trip.Id));
    }

    [Fact]
    public async Task Concurrent_serializable_owner_demotions_report_a_conflict_and_retain_an_owner()
    {
        await using var seed = CreateDatabase();
        var tripId = TripId.New();
        var firstOwner = Membership.Create(tripId, "postgres-owner-1", MembershipRole.Owner);
        var secondOwner = Membership.Create(tripId, "postgres-owner-2", MembershipRole.Owner);
        seed.Memberships.AddRange(firstOwner, secondOwner);
        await seed.SaveChangesAsync();

        var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        async Task<Exception?> DemoteAsync(MembershipId id)
        {
            await using var database = CreateDatabase();
            return await Record.ExceptionAsync(() => new EfUnitOfWork(database).ExecuteSerializableAsync(async cancellationToken =>
            {
                var members = await database.Memberships.Where(member => member.TripId == tripId).ToListAsync(cancellationToken);
                Assert.Equal(2, members.Count(member => member.Role == MembershipRole.Owner));
                if (Interlocked.Increment(ref readers) == 2)
                {
                    bothRead.SetResult();
                }

                await bothRead.Task.WaitAsync(cancellationToken);
                members.Single(member => member.Id == id).ChangeRole(MembershipRole.Viewer);
                await database.SaveChangesAsync(cancellationToken);
            }, timeout.Token));
        }

        var results = await Task.WhenAll(DemoteAsync(firstOwner.Id), DemoteAsync(secondOwner.Id));
        Assert.Single(results, exception => exception is null);
        Assert.Single(results, exception => exception is ConcurrencyConflictException);
        seed.ChangeTracker.Clear();
        Assert.Equal(1, await seed.Memberships.CountAsync(member => member.TripId == tripId && member.Role == MembershipRole.Owner));
    }

    [Fact]
    public async Task Initialized_database_is_ready_and_connection_failure_is_not_ready()
    {
        using var factory = new TripSideKickApplicationFactory { SqlConnectionStringOverride = fixture.ConnectionString };
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);
        using var response = await client.GetAsync(new Uri("/api/health/ready", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var unavailable = new TripSideKickApplicationFactory
        {
            SqlConnectionStringOverride = "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Timeout=1"
        };
        using var unavailableClient = unavailable.CreateClientFor(TripSideKickApplicationFactory.AppHost);
        using var failed = await unavailableClient.GetAsync(new Uri("/api/health/ready", UriKind.Relative));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
    }
}
