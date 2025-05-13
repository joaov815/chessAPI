using System.Net.WebSockets;
using System.Text.Json;
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

    public async Task<Match> OnUserConnected(User user)
    {
        Match? ongoingMatch = await GetUserOngoingMatch(user);

        Console.WriteLine(JsonSerializer.Serialize(ongoingMatch));

        if (ongoingMatch != null)
        {
            return ongoingMatch;
        }

        Match? match = await GetMatchMakingMatch(user);

        if (match is null)
        {
            match = await CreateAsync(user);
        }
        else
        {
            await StartMatch(user, match);
        }

        return match!;
    }

    public async Task<Match> CreateAsync(User user)
    {
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

        await Context.SaveChangesAsync();

        return match;
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

    public async Task<Match?> GetUserOngoingMatch(User user)
    {
        return await QueryBuilder
            .Include(_ => _.BlackUser)
            .Include(_ => _.WhiteUser)
            .FirstOrDefaultAsync(m =>
                m.Status == MatchStatusEnum.ONGOING
                && (
                    (m.BlackUser != null && m.BlackUser.Id == user.Id)
                    || (m.WhiteUser != null && m.WhiteUser.Id == user.Id)
                )
            );
    }

    public async Task<MatchMakingResponseDto> OnMatchMaking(WebSocket webSocket, WsMessageDto dto)
    {
        User user = await _userService.CreateIfNotExistsAsync(new() { Username = dto.Username });

        _manager.AddClient(webSocket, user);

        var match = await OnUserConnected(user);

        return new MatchMakingResponseDto { MatchId = match.Id, User = user };
    }

    public async Task OnMove(
        MatchMakingResponseDto matchMakingResponse,
        WsMovePieceDto movePieceDto
    )
    {
        Console.WriteLine(JsonSerializer.Serialize(matchMakingResponse));
        Console.WriteLine(JsonSerializer.Serialize(movePieceDto));
    }
}
