using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagement.Members.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "members");

            migrationBuilder.CreateTable(
                name: "Member",
                schema: "members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CardNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MembershipStart = table.Column<DateOnly>(type: "date", nullable: false),
                    MembershipEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    ErasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PostalAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Guardian_Present = table.Column<bool>(type: "bit", nullable: true),
                    GuardianEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    GuardianPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    GuardianPostalAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GuardianFamilyName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GuardianGivenName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FamilyName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GivenName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Name_Present = table.Column<bool>(type: "bit", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Member", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "members",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    StoredOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ProcessedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeadOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Member_CardNumber",
                schema: "members",
                table: "Member",
                column: "CardNumber",
                unique: true,
                filter: "[CardNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EventId",
                schema: "members",
                table: "OutboxMessage",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Pending",
                schema: "members",
                table: "OutboxMessage",
                column: "Id",
                filter: "[ProcessedOn] IS NULL AND [DeadOn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Member",
                schema: "members");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "members");
        }
    }
}
