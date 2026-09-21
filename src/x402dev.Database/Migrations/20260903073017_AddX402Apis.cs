using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace x402dev.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddX402Apis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "X402Apis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    PaymentsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AddedDateTime = table.Column<long>(type: "INTEGER", nullable: false),
                    LastCheckDateTime = table.Column<long>(type: "INTEGER", nullable: true),
                    NextCheckDateTime = table.Column<long>(type: "INTEGER", nullable: true),
                    LastSuccessDateTime = table.Column<long>(type: "INTEGER", nullable: true),
                    LastErrorDateTime = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    RawJsonResponse = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_X402Apis", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "X402Apis");
        }
    }
}
