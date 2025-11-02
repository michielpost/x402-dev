using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace x402dev.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Payer = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Transaction = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Network = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Asset = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Value = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Link = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreatedDateTime = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicMessages", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicMessages");
        }
    }
}
