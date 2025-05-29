using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessAPI.Migrations
{
    /// <inheritdoc />
    public partial class PinnedPiece : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Pieces");

            migrationBuilder.AddColumn<int>(
                name: "PinnedById",
                table: "Pieces",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pieces_PinnedById",
                table: "Pieces",
                column: "PinnedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Pieces_Pieces_PinnedById",
                table: "Pieces",
                column: "PinnedById",
                principalTable: "Pieces",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pieces_Pieces_PinnedById",
                table: "Pieces");

            migrationBuilder.DropIndex(
                name: "IX_Pieces_PinnedById",
                table: "Pieces");

            migrationBuilder.DropColumn(
                name: "PinnedById",
                table: "Pieces");

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Pieces",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
