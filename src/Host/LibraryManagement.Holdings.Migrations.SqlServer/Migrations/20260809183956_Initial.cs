using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagement.Holdings.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "holdings");

            migrationBuilder.CreateTable(
                name: "Copy",
                schema: "holdings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Shelfmark = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReturnsTo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    AcquiredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Copy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "holdings",
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
                name: "IX_Copy_Barcode",
                schema: "holdings",
                table: "Copy",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Copy_EditionId",
                schema: "holdings",
                table: "Copy",
                column: "EditionId");

            migrationBuilder.CreateIndex(
                name: "IX_Copy_Shelfmark",
                schema: "holdings",
                table: "Copy",
                column: "Shelfmark");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EventId",
                schema: "holdings",
                table: "OutboxMessage",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Pending",
                schema: "holdings",
                table: "OutboxMessage",
                column: "Id",
                filter: "[ProcessedOn] IS NULL AND [DeadOn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Copy",
                schema: "holdings");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "holdings");
        }
    }
}
