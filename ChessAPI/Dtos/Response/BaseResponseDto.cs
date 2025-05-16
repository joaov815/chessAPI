using ChessAPI.Enums;

namespace ChessAPI.Dtos.Response;

public class BaseResponseDto
{
    public WsMessageTypeResponseEnum Type { get; set; }
}
