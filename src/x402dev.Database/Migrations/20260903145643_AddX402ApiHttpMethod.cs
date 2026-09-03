using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace x402dev.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddX402ApiHttpMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "X402Apis",
                type: "TEXT",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "X402Apis");
        }
    }
}
