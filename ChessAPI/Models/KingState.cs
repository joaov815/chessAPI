using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessAPI.Models;

public class KingState
{
    [Key]
    public int Id { get; set; }
    public required Piece Piece { get; set; }
    public required List<string> PositionsAround { get; set; }
    public required List<string> OpponentPositionsAround { get; set; }

    [NotMapped]
    public List<string> NonEnemyPositions
    {
        get => [.. PositionsAround.Where(p => !OpponentPositionsAround.Contains(p))];
    }
}
