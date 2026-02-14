using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InterfazInAction.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyNodeName",
                table: "integrationProcesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XmlTemplate",
                table: "integrationProcesses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyNodeName",
                table: "integrationProcesses");

            migrationBuilder.DropColumn(
                name: "XmlTemplate",
                table: "integrationProcesses");
        }
    }
}
