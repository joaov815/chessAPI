using ChessAPI.Enums;

namespace ChessAPI.Dtos.Response;

public class AvailablePositionsDto : BaseResponseDto
{
    public AvailablePositionsDto()
    {
        Type = WsMessageTypeResponseEnum.AVAILABLE_POSITIONS;
    }

    public required List<string> Positions { get; set; }
}
