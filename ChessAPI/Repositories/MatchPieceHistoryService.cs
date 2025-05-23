using ChessAPI.Data;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Repositories;

public class MatchPieceHistoryRepository(AppDbContext context)
    : BaseRepository<MatchPieceHistory>(context)
{
    public Task<List<MatchPieceHistory>> GetMatchHistory(
        int matchId,
        AppDbContext? currentDbContext
    )
    {
        var query = GetQueryBuilder(currentDbContext ?? Context);

        return query
            .Include(_ => _.Piece)
            .Where(_ => _.Match.Id == matchId)
            .OrderByDescending((_) => _.CreatedAt)
            .ToListAsync();
    }

    public Task<MatchPieceHistory?> GetMatchLastPieceHistory(
        int matchId,
        AppDbContext? currentDbContext
    )
    {
        var query = GetQueryBuilder(currentDbContext ?? Context);

        return query
            .Include(_ => _.Piece)
            .Where((_) => _.Match.Id == matchId)
            .OrderByDescending((_) => _.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public MatchPieceHistory Save(
        MatchPieceHistory matchPieceHistory,
        AppDbContext? currentDbContext
    )
    {
        var dbSet = GetDbSet(currentDbContext ?? Context);

        dbSet.Add(matchPieceHistory);

        return matchPieceHistory;
    }
}
