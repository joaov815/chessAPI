using ChessAPI.Enums;

namespace ChessAPI.Dtos;

public class BaseResponseDto
{
    public required MatchResponseTypeEnum Type { get; set; }
}
