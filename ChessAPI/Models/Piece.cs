using System.ComponentModel.DataAnnotations.Schema;
using ChessAPI.Enums;

namespace ChessAPI.Models;

public class Piece
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public required BoardSideEnum InitialBoardSide { get; set; }
    public required Match Match { get; set; }
    public required PieceColorEnum Color { get; set; }
    public required PieceEnum Value { get; set; }
    public required int Column { get; set; }
    public required int Row { get; set; }
    public required bool WasCaptured { get; set; } = false;
    public Piece? PinnedBy { get; set; }
    public Piece? pinnedBy;

    [NotMapped]
    public string? EnPassantCapturePosition { get; set; }

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

    public void UpdatePosition(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public void SetPinned(Piece piece)
    {
        pinnedBy = piece;
    }

    public void UpdatePinned()
    {
        PinnedBy = pinnedBy;
    }
}
