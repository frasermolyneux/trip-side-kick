using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MX.TripSideKick.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItineraryAndTravellerFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItineraryComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItineraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItineraryComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItineraryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApplicableTravellerIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ScheduledEndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ScheduledStartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ScheduleStatus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItineraryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TripActivityFeedEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItineraryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripActivityFeedEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TripTravellerFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    SelectedTravellerIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripTravellerFilters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItineraryComments_ItineraryItemId",
                table: "ItineraryComments",
                column: "ItineraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItineraryComments_TripId",
                table: "ItineraryComments",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_ItineraryItems_TripId",
                table: "ItineraryItems",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripActivityFeedEntries_TripId_OccurredAt",
                table: "TripActivityFeedEntries",
                columns: new[] { "TripId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TripTravellerFilters_TripId_MembershipId",
                table: "TripTravellerFilters",
                columns: new[] { "TripId", "MembershipId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItineraryComments");

            migrationBuilder.DropTable(
                name: "ItineraryItems");

            migrationBuilder.DropTable(
                name: "TripActivityFeedEntries");

            migrationBuilder.DropTable(
                name: "TripTravellerFilters");
        }
    }
}
