using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InterfazInAction.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationProcesses",
                columns: table => new
                {
                    ProcessName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InterfaceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetTable = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    XmlIterator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationProcesses", x => x.ProcessName);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProcessName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    XmlPath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DbColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    ReferenceTable = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceColumn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsKey = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationFields_IntegrationProcesses_ProcessName",
                        column: x => x.ProcessName,
                        principalTable: "IntegrationProcesses",
                        principalColumn: "ProcessName",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFields_ProcessName",
                table: "IntegrationFields",
                column: "ProcessName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationFields");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "IntegrationProcesses");
        }
    }
}
