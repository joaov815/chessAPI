using ChessAPI.Models;

namespace ChessAPI.Dtos;

public class MatchMakingResponseDto
{
    public required User User { get; set; }
    public int MatchId { get; set; }
}
