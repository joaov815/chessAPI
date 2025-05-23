using ChessAPI.Data;
using ChessAPI.Enums;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Repositories;

public class MatchRepository(AppDbContext context) : BaseRepository<Match>(context)
{
    public async Task<Match> CreateAsync(User user, AppDbContext currentContext)
    {
        var _context = currentContext ?? Context;
        var _dbSet = GetDbSet(_context) ?? dbSet;

        Match match = new()
        {
            Status = MatchStatusEnum.MATCHMAKING,
            SecondsDuration = 10 * 1000,
            Rounds = 0,
        };

        var random = new Random();

        if (random.Next(0, 1) == 1)
        {
            match.WhiteUser = user;
        }
        else
        {
            match.BlackUser = user;
        }

        _dbSet.Add(match);

        await _context.SaveChangesAsync();

        return match;
    }

    public async Task<Match> GetOngoingByIdAsync(int matchId, AppDbContext currentContext)
    {
        var query = GetQueryBuilder(currentContext ?? Context);

        return await query
                .Include(_ => _.BlackUser)
                .Include(_ => _.WhiteUser)
                .FirstOrDefaultAsync((_) => _.Id == matchId && _.Status == MatchStatusEnum.ONGOING)
            ?? throw new Exception("Not found");
    }

    public async Task<Match?> GetMatchMakingMatch(User user, AppDbContext currentContext)
    {
        var query = GetQueryBuilder(currentContext ?? Context);

        return await query
            .Include(_ => _.BlackUser)
            .Include(_ => _.WhiteUser)
            .FirstOrDefaultAsync(m =>
                m.Status == MatchStatusEnum.MATCHMAKING
                && (m.BlackUser == null || m.BlackUser.Id != user.Id)
                && (m.WhiteUser == null || m.WhiteUser.Id != user.Id)
            );
    }

    public async Task<Match?> GetMyUnfinishedMatch(User user, AppDbContext currentContext)
    {
        var query = GetQueryBuilder(currentContext ?? Context);

        return await query
            .Include(_ => _.BlackUser)
            .Include(_ => _.WhiteUser)
            .FirstOrDefaultAsync(m =>
                (m.Status == MatchStatusEnum.ONGOING || m.Status == MatchStatusEnum.MATCHMAKING)
                && (
                    (m.BlackUser != null && m.BlackUser.Id == user.Id)
                    || (m.WhiteUser != null && m.WhiteUser.Id == user.Id)
                )
            );
    }
}
