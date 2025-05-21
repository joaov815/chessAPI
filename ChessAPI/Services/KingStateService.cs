using ChessAPI.Data;
using ChessAPI.Enums;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class KingStateService
{
    public KingStateService(AppDbContext context)
    {
        Context = context;
        _dbSet = context.Set<KingState>();
        QueryBuilder = _dbSet.AsQueryable();
    }

    protected readonly DbSet<KingState> _dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<KingState> QueryBuilder { get; set; }

    public async Task<KingState> GetByPieceId(int pieceId)
    {
        return await QueryBuilder.FirstOrDefaultAsync(_ => _.Piece.Id == pieceId)
            ?? throw new Exception("NOT FOUND");
    }

    public async Task<KingState> GetOpponentKing(int matchId, PieceColorEnum pieceColor)
    {
        return await QueryBuilder
                .Include(k => k.Piece)
                .Where((k) => k.Piece.Match.Id == matchId && k.Piece.Color != pieceColor)
                .FirstOrDefaultAsync() ?? throw new Exception("NOT FOUND");
    }

    public void AddKingsStates(List<Piece> kings)
    {
        _dbSet.AddRange(
            kings.Select(piece => new KingState()
            {
                OpponentPositionsAround = [],
                Piece = piece,
                PositionsAround = GetPositionsAround(piece),
            })
        );
    }

    private static List<string> GetPositionsAround(Piece piece)
    {
        int[][] directions =
        [
            [-1, -1],
            [1, -1],
            [-1, 1],
            [1, 1],
            [-1, 0],
            [1, 0],
            [0, -1],
            [0, 1],
        ];

        return
        [
            .. directions.Select(direction =>
                $"{piece.Row + direction[0]}{piece.Column + direction[1]}"
            ),
        ];
    }
}
