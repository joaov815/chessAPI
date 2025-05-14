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
}
