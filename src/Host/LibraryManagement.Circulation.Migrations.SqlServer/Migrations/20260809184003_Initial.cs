using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagement.Circulation.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "circulation");

            migrationBuilder.CreateTable(
                name: "HoldQueue",
                schema: "circulation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoldQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Loan",
                schema: "circulation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CopyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BorrowerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckedOutOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RenewalCount = table.Column<int>(type: "int", nullable: false),
                    ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DeclaredLostOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RecoveredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "circulation",
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

            migrationBuilder.CreateTable(
                name: "Hold",
                schema: "circulation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BorrowerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlacedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TrappedCopyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PickupDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryWarningSent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hold", x => new { x.EditionId, x.Id });
                    table.ForeignKey(
                        name: "FK_Hold_HoldQueue_EditionId",
                        column: x => x.EditionId,
                        principalSchema: "circulation",
                        principalTable: "HoldQueue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoanReminderSent",
                schema: "circulation",
                columns: table => new
                {
                    DaysFromDue = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanReminderSent", x => new { x.LoanId, x.DaysFromDue });
                    table.ForeignKey(
                        name: "FK_LoanReminderSent_Loan_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "circulation",
                        principalTable: "Loan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hold_BorrowerId",
                schema: "circulation",
                table: "Hold",
                column: "BorrowerId");

            migrationBuilder.CreateIndex(
                name: "IX_Hold_TrappedCopyId",
                schema: "circulation",
                table: "Hold",
                column: "TrappedCopyId",
                unique: true,
                filter: "[TrappedCopyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Loan_BorrowerId",
                schema: "circulation",
                table: "Loan",
                column: "BorrowerId");

            migrationBuilder.CreateIndex(
                name: "IX_Loan_CopyId",
                schema: "circulation",
                table: "Loan",
                column: "CopyId",
                unique: true,
                filter: "[Status] = N'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EventId",
                schema: "circulation",
                table: "OutboxMessage",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Pending",
                schema: "circulation",
                table: "OutboxMessage",
                column: "Id",
                filter: "[ProcessedOn] IS NULL AND [DeadOn] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hold",
                schema: "circulation");

            migrationBuilder.DropTable(
                name: "LoanReminderSent",
                schema: "circulation");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "circulation");

            migrationBuilder.DropTable(
                name: "HoldQueue",
                schema: "circulation");

            migrationBuilder.DropTable(
                name: "Loan",
                schema: "circulation");
        }
    }
}
