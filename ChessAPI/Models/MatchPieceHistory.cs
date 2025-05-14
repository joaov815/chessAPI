using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessAPI.Models;

public class MatchPieceHistory
{
    [Key]
    public int Id { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required Match Match { get; set; }
    public required Piece Piece { get; set; }
    public required int Round { get; set; }
    public required int CurrentRow { get; set; }
    public required int CurrentColumn { get; set; }
    public required int PreviousRow { get; set; }
    public required int PreviousColumn { get; set; }

    [NotMapped]
    public string CurrentPosition
    {
        get => CurrentRow.ToString() + CurrentColumn.ToString();
    }

    [NotMapped]
    public string PreviousPosition
    {
        get => PreviousRow.ToString() + PreviousColumn.ToString();
    }
}
