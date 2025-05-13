using ChessAPI.Enums;

namespace ChessAPI.Dtos;

public class WsMessageDto
{
    public required string Username { get; set; }
    public required WsMessageTypeEnum Type { get; set; }
}
