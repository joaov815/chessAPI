using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessAPI.Migrations
{
    /// <inheritdoc />
    public partial class PiecePositionsUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pieces_MatchId",
                table: "Pieces");

            migrationBuilder.CreateIndex(
                name: "IX_Pieces_MatchId_Row_Column",
                table: "Pieces",
                columns: new[] { "MatchId", "Row", "Column" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pieces_MatchId_Row_Column",
                table: "Pieces");

            migrationBuilder.CreateIndex(
                name: "IX_Pieces_MatchId",
                table: "Pieces",
                column: "MatchId");
        }
    }
}
