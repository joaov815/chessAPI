using ChessAPI.Enums;
using ChessAPI.Models;

namespace ChessAPI.Dtos.Response;

public class MatchMakingResponseDto
{
    public required User User { get; set; }
    public required PieceColorEnum Color { get; set; }
    public required int MatchId { get; set; }
}
