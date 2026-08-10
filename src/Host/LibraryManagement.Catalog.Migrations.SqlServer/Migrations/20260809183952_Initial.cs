using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryManagement.Catalog.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "AccessPoint",
                schema: "catalog",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Form = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsPreferred = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessPoint", x => new { x.Kind, x.TargetId, x.Form });
                });

            migrationBuilder.CreateTable(
                name: "Author",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreferredName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BirthYear = table.Column<int>(type: "int", nullable: true),
                    DeathYear = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Author", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Edition",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Edition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "catalog",
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
                name: "Work",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreferredTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Work", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthorVariantName",
                schema: "catalog",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorVariantName", x => new { x.AuthorId, x.Name });
                    table.ForeignKey(
                        name: "FK_AuthorVariantName_Author_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "catalog",
                        principalTable: "Author",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkAuthor",
                schema: "catalog",
                columns: table => new
                {
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkAuthor", x => new { x.WorkId, x.AuthorId });
                    table.ForeignKey(
                        name: "FK_WorkAuthor_Work_WorkId",
                        column: x => x.WorkId,
                        principalSchema: "catalog",
                        principalTable: "Work",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessPoint_Form",
                schema: "catalog",
                table: "AccessPoint",
                column: "Form");

            migrationBuilder.CreateIndex(
                name: "IX_Author_PreferredName",
                schema: "catalog",
                table: "Author",
                column: "PreferredName");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorVariantName_Name",
                schema: "catalog",
                table: "AuthorVariantName",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Edition_Isbn",
                schema: "catalog",
                table: "Edition",
                column: "Isbn");

            migrationBuilder.CreateIndex(
                name: "IX_Edition_WorkId",
                schema: "catalog",
                table: "Edition",
                column: "WorkId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EventId",
                schema: "catalog",
                table: "OutboxMessage",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Pending",
                schema: "catalog",
                table: "OutboxMessage",
                column: "Id",
                filter: "[ProcessedOn] IS NULL AND [DeadOn] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Work_PreferredTitle",
                schema: "catalog",
                table: "Work",
                column: "PreferredTitle");

            migrationBuilder.CreateIndex(
                name: "IX_WorkAuthor_AuthorId",
                schema: "catalog",
                table: "WorkAuthor",
                column: "AuthorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessPoint",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "AuthorVariantName",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Edition",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "WorkAuthor",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Author",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Work",
                schema: "catalog");
        }
    }
}
