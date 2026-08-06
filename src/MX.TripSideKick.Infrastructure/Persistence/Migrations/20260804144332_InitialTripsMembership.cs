using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MX.TripSideKick.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTripsMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LinkKind = table.Column<int>(type: "int", nullable: false),
                    ExistingTravellerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewTravellerDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcceptanceToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Travellers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LinkedMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Travellers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Destinations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReportingCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DateStatus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptanceToken",
                table: "Invitations",
                column: "AcceptanceToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TripId",
                table: "Invitations",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_SubjectId",
                table: "Memberships",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TripId_SubjectId",
                table: "Memberships",
                columns: new[] { "TripId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Travellers_LinkedMembershipId",
                table: "Travellers",
                column: "LinkedMembershipId",
                unique: true,
                filter: "[LinkedMembershipId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Travellers_TripId",
                table: "Travellers",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "Memberships");

            migrationBuilder.DropTable(
                name: "Travellers");

            migrationBuilder.DropTable(
                name: "Trips");
        }
    }
}
