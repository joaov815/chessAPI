using ChessAPI.Extensions;
using ChessAPI.Models;

namespace ChessAPI.Utils;

public class PositionUtils
{
    public static bool IsValidPosition(string position)
    {
        return position.PositionToRow() > 0
            && position.PositionToRow() < 7
            && position.PositionToColumn() > 0
            && position.PositionToColumn() < 7;
    }

    public static List<string> GetPositionsAround(Piece piece)
    {
        int[][] directions =
        [
            [-1, -1],
            [0, -1],
            [1, -1],
            [1, 0],
            [1, 1],
            [0, 1],
            [-1, 1],
            [-1, 0],
        ];

        return
        [
            .. directions
                .Select(direction =>
                    (int[])[(piece.Row + direction[0]), (piece.Column + direction[1])]
                )
                .Where(direction =>
                    direction[0] >= 0 && direction[0] < 8 && direction[1] >= 0 && direction[1] < 8
                )
                .Select(direction => $"{direction[0]}{direction[1]}"),
        ];
    }
}
