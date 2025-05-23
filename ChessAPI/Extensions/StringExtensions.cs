namespace ChessAPI.Extensions;

public static class StringExtensions
{
    public static int PositionToRow(this string value)
    {
        string position = value[0] == '-' ? value[..2] : value[0].ToString();

        Console.WriteLine(position);

        return int.Parse(position);
    }

    public static int PositionToColumn(this string value)
    {
        string position = value[^1].ToString();

        if (value.Length == 3 && value[1] == '-' || value.Length == 4)
        {
            position = $"-{position}";
        }

        return int.Parse(position);
    }

    public static bool IsValidPosition(this string position)
    {
        var row = position.PositionToRow();
        var column = position.PositionToColumn();

        return row > 0 && row < 8 && column > 0 && column < 8;
    }
}
