namespace ChessAPI.Dtos;

public class WsMovePieceDto
{
    public required int FromRow { get; set; }
    public required int FromColumn { get; set; }
    public required int ToColumn { get; set; }
    public required int ToRow { get; set; }

    public bool IsValid()
    {
        return !((FromRow == ToRow && FromColumn == ToColumn) || ToColumn > 7 || ToRow > 7);
    }

    public string ToPosition
    {
        get => $"{ToRow}{ToColumn}";
    }

    public string FromPosition
    {
        get => $"{FromRow}{FromColumn}";
    }
}
