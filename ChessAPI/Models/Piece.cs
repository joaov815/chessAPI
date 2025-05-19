using System.ComponentModel.DataAnnotations.Schema;
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

    [NotMapped]
    public string? OnPassantCapturePosition { get; set; }

    [NotMapped]
    public bool IsWhite
    {
        get => Color == PieceColorEnum.WHITE;
    }

    [NotMapped]
    public string Position
    {
        get => Row.ToString() + Column.ToString();
    }

    public bool IsOponents(Piece piece)
    {
        return Color != piece.Color;
    }

    public void Promote(PieceEnum value)
    {
        // TODO: //
    }
}
