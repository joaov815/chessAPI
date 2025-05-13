using ChessAPI.Enums;

namespace ChessAPI.Models;

public class Piece
{
    public int Id { get; set; }
    public required Match Match { get; set; }
    public required PieceColorEnum Color { get; set; }
    public required PieceEnum Value { get; set; }
    public required int Column { get; set; }
    public required int Row { get; set; }
    public required bool WasCaptured { get; set; } = false;

    public void Promote(PieceEnum value)
    {
        // TODO: //
    }
}
