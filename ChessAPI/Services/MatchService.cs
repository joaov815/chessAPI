using ChessAPI.Data;
using ChessAPI.Enums;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class MatchService
{
    protected readonly DbSet<Match> _dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<Match> QueryBuilder { get; set; }

    public MatchService(AppDbContext context)
    {
        Context = context;
        _dbSet = context.Set<Match>();
        QueryBuilder = _dbSet.AsQueryable();
    }

    public async Task CreateAsync(User user)
    {
        Match match = new() { Status = MatchStatusEnum.MATCHMAKING, SecondsDuration = 10 * 1000 };

        // TODO: checar se usuario nao esta em outra partida

        // Setar aleatoriamente se jogador vai sair com brancas ou pretas
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

        await Context.SaveChangesAsync();
    }

    public async Task StartMatch(User user, Match match)
    {
        match.SetSecondPlayer(user);
        match.StartedAt = DateTime.Now;
        match.Status = MatchStatusEnum.ONGOING;

        await Context.SaveChangesAsync();
    }

    public async Task<Match?> GetMatchMakingMatch()
    {
        return await QueryBuilder.FirstOrDefaultAsync(m => m.Status == MatchStatusEnum.MATCHMAKING);
    }
}
