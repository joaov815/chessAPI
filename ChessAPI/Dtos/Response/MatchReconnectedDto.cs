using ChessAPI.Enums;
using ChessAPI.Models;

namespace ChessAPI.Dtos.Response;

public class MatchReconnectedDto : BaseResponseDto
{
    public MatchReconnectedDto()
    {
        Type = WsMessageTypeResponseEnum.RECONNECTED;
    }

    public required List<Piece> Pieces { get; set; }
    public required PieceColorEnum Color { get; set; }
}
