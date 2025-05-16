namespace ChessAPI.Dtos;

public class GetPiecePositionsDto
{
    public required int Column { get; set; }
    public required int Row { get; set; }

    public string Position
    {
        get => $"{Row}{Column}";
    }
}
