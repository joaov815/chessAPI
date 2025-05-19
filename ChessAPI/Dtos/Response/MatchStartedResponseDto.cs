using ChessAPI.Enums;

namespace ChessAPI.Dtos.Response;

public class MatchStartedResponseDto : BaseResponseDto
{
    public required PieceColorEnum Color { get; set; }
    public string? BlackUsername { get; set; }
    public string? WhiteUsername { get; set; }
}
