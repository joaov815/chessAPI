using ChessAPI.Models;

namespace ChessAPI.Dtos.Response;

public class MoveResponseDto : BaseResponseDto
{
    public required MatchPieceHistory History { get; set; }
    public WsMovePieceDto? CastleRookMove { get; set; }
    public string? CapturedEnPassantPawn { get; set; }
}
