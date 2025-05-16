using ChessAPI.Models;

namespace ChessAPI.Dtos.Response;

public class MoveResponseDto : BaseResponseDto
{
    public required MatchPieceHistory MatchPieceHistory;
}
