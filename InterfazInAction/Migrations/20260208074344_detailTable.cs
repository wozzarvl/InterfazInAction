using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterfazInAction.Migrations
{
    /// <inheritdoc />
    public partial class detailTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailNodeName",
                table: "integrationProcesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailTable",
                table: "integrationProcesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailNodeName",
                table: "integrationProcesses");

            migrationBuilder.DropColumn(
                name: "DetailTable",
                table: "integrationProcesses");
        }
    }
}
