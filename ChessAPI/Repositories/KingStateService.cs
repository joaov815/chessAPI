using ChessAPI.Data;
using ChessAPI.Enums;
using ChessAPI.Models;
using ChessAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Repositories;

public class KingStateRepository(AppDbContext context) : BaseRepository<KingState>(context)
{
    public async Task<KingState> GetByPieceId(int pieceId, AppDbContext? currentContext)
    {
        var query = GetQueryBuilder(currentContext ?? Context);

        return await query.Include(_ => _.Piece).FirstOrDefaultAsync(_ => _.Piece.Id == pieceId)
            ?? throw new Exception("NOT FOUND");
    }

    public async Task<KingState> GetOpponentKing(
        int matchId,
        PieceColorEnum pieceColor,
        AppDbContext? currentContext
    )
    {
        var query = GetQueryBuilder(currentContext ?? Context);

        return await query
                .Include(k => k.Piece)
                .Where((k) => k.Piece.Match.Id == matchId && k.Piece.Color != pieceColor)
                .FirstOrDefaultAsync() ?? throw new Exception("NOT FOUND");
    }

    public void AddKingsStates(List<Piece> kings, AppDbContext? currentContext)
    {
        var _dbSet = GetDbSet(currentContext ?? Context);

        _dbSet.AddRange(
            kings
                .Select(piece => new KingState()
                {
                    OpponentPositionsAround = [],
                    Piece = piece,
                    PositionsAround = PositionUtils.GetPositionsAround(piece),
                })
                .ToList()
        );
    }
}
