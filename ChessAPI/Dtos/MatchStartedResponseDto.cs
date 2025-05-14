using ChessAPI.Enums;

namespace ChessAPI.Dtos;

public class MatchStartedResponseDto : BaseResponseDto
{
    public required PieceColorEnum Color { get; set; }
}
