using ChessAPI.Enums;

namespace ChessAPI.Dtos;

public class WsMovePieceDto
{
    public required int PieceId { get; set; }
    public required ColumnEnum ToColumn { get; set; }
    public required RowEnum ToRow { get; set; }
    public required WsMessageTypeEnum Type { get; set; }
}
