using ChessAPI.Data;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class MatchPieceHistoryService
{
    public MatchPieceHistoryService(AppDbContext context, WebSocketConnectionManager manager)
    {
        Context = context;
        _dbSet = context.Set<MatchPieceHistory>();
        QueryBuilder = _dbSet.AsQueryable();
        _manager = manager;
    }

    private readonly WebSocketConnectionManager _manager;

    protected readonly DbSet<MatchPieceHistory> _dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<MatchPieceHistory> QueryBuilder { get; set; }

    public Task<MatchPieceHistory?> GetMatchLastPieceHistory(int matchId)
    {
        return QueryBuilder
            .Include(_ => _.Piece)
            .Where((_) => _.Match.Id == matchId)
            .OrderByDescending((_) => _.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public void Save(MatchPieceHistory matchPieceHistory)
    {
        _dbSet.Add(matchPieceHistory);
    }
}
