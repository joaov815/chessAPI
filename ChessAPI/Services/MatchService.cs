using System.Net.WebSockets;
using ChessAPI.Data;
using ChessAPI.Dtos;
using ChessAPI.Enums;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class MatchService
{
    public MatchService(
        AppDbContext context,
        UserService userService,
        WebSocketConnectionManager manager
    )
    {
        Context = context;
        _dbSet = context.Set<Match>();
        QueryBuilder = _dbSet.AsQueryable();

        _userService = userService;
        _manager = manager;
    }

    private readonly WebSocketConnectionManager _manager;

    protected readonly DbSet<Match> _dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<Match> QueryBuilder { get; set; }

    private readonly UserService _userService;

    public async Task OnUserConnected(User user)
    {
        Match? ongoindMatch = await GetUserMatchMakingMatch(user);

        if (ongoindMatch != null)
        {
            return;
        }

        Match? match = await GetMatchMakingMatch(user);

        if (match is null)
        {
            await CreateAsync(user);
        }
        else
        {
            await StartMatch(user, match);
        }
    }

    public async Task CreateAsync(User user)
    {
        Match match = new()
        {
            Status = MatchStatusEnum.MATCHMAKING,
            SecondsDuration = 10 * 1000,
            Rounds = 0,
        };

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

    public List<Piece> SetInitialBoard(Match match)
    {
        List<Piece> pieces = [];

        for (int i = 0; i < 16; i++)
        {
            bool isWhite = i < 8;

            pieces.Add(
                new()
                {
                    Value = PieceEnum.PAWN,
                    Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                    Column = (ColumnEnum)i,
                    Row = isWhite ? RowEnum.TWO : RowEnum.SEVEN,
                    WasCaptured = false,
                    Match = match,
                }
            );
        }

        PieceEnum[] piecesValues = [PieceEnum.HOOK, PieceEnum.KNIGHT, PieceEnum.BISHOP];

        for (int j = 0; j < piecesValues.Length; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                bool isWhite = i < 2;

                pieces.Add(
                    new()
                    {
                        Value = piecesValues[j],
                        Column = (ColumnEnum)(i % 2 == 0 ? j : (7 - j)),
                        Row = isWhite ? RowEnum.ONE : RowEnum.EIGHT,
                        Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                        WasCaptured = false,
                        Match = match,
                    }
                );
            }
        }

        for (int i = 0; i < 4; i++)
        {
            bool isWhite = i < 2;

            pieces.Add(
                new()
                {
                    Value = isWhite ? PieceEnum.QUEEN : PieceEnum.KING,
                    Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                    Column = (ColumnEnum)(isWhite ? 3 : 4),
                    Row = isWhite ? RowEnum.ONE : RowEnum.EIGHT,
                    WasCaptured = false,
                    Match = match,
                }
            );
        }

        return pieces;
    }

    public async Task StartMatch(User user, Match match)
    {
        match.SetSecondPlayer(user);
        match.StartedAt = DateTime.UtcNow;
        match.Status = MatchStatusEnum.ONGOING;

        await Context.SaveChangesAsync();

        Console.WriteLine("Game started");
    }

    public async Task<Match?> GetMatchMakingMatch(User user)
    {
        return await QueryBuilder
            .Include(_ => _.BlackUser)
            .Include(_ => _.WhiteUser)
            .FirstOrDefaultAsync(m =>
                m.Status == MatchStatusEnum.MATCHMAKING
                && (m.BlackUser == null || m.BlackUser.Id != user.Id)
                && (m.WhiteUser == null || m.WhiteUser.Id != user.Id)
            );
    }

    public async Task<Match?> GetUserMatchMakingMatch(User user)
    {
        return await QueryBuilder
            .Include(_ => _.BlackUser)
            .Include(_ => _.WhiteUser)
            .FirstOrDefaultAsync(m =>
                m.Status == MatchStatusEnum.MATCHMAKING
                && (
                    (m.BlackUser != null && m.BlackUser.Id == user.Id)
                    || (m.WhiteUser != null && m.WhiteUser.Id == user.Id)
                )
            );
    }

    public async Task<User> OnMatchMaking(WebSocket webSocket, WsMessageDto dto)
    {
        User user =
            await _userService.CreateAsync(new() { Username = dto.Username })
            ?? throw new InvalidOperationException("User not found");

        _manager.AddClient(webSocket, user);

        await OnUserConnected(user);

        Console.WriteLine($"{user.Username} no matchmaking....");

        return user;
    }

    public async Task OnMove(User user, WsMovePieceDto movePieceDto)
    {
        // TODO:
    }
}
