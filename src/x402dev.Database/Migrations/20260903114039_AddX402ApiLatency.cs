using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace x402dev.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddX402ApiLatency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LatencyMs",
                table: "X402Apis",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatencyMs",
                table: "X402Apis");
        }
    }
}
