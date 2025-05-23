using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessAPI.Migrations
{
    /// <inheritdoc />
    public partial class PieceInitialSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InitialBoardSide",
                table: "Pieces",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialBoardSide",
                table: "Pieces");
        }
    }
}
