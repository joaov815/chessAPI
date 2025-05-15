using ChessAPI.Enums;

namespace ChessAPI.Dtos;

public class BaseResponseDto
{
    public required WsMessageTypeResponseEnum Type { get; set; }
}
