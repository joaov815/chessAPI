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
        PieceService pieceService,
        WebSocketConnectionManager manager
    )
    {
        Context = context;
        _dbSet = context.Set<Match>();
        _piecesDbSet = context.Set<Piece>();
        QueryBuilder = _dbSet.AsQueryable();

        _userService = userService;
        _pieceService = pieceService;
        _manager = manager;
    }

    private readonly WebSocketConnectionManager _manager;

    protected readonly DbSet<Match> _dbSet;
    protected readonly DbSet<Piece> _piecesDbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<Match> QueryBuilder { get; set; }

    private readonly UserService _userService;
    private readonly PieceService _pieceService;

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

    public async Task StartMatch(User user, Match match)
    {
        match.SetSecondPlayer(user);
        match.StartedAt = DateTime.UtcNow;
        match.Status = MatchStatusEnum.ONGOING;

        _pieceService.SetInitialBoard(match);

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
                || m.Status == MatchStatusEnum.MATCHMAKING
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

        return new MatchMakingResponseDto
        {
            MatchId = match.Id,
            User = user,
            Color = match.BlackUser != null ? PieceColorEnum.BLACK : PieceColorEnum.WHITE,
        };
    }

    public async Task OnMove(MatchMakingResponseDto my, WsMovePieceDto dto)
    {
        if (!dto.IsValid())
        {
            return;
        }

        List<Piece> pieces = await _pieceService.GetMatchActivePiecesAsync(my.MatchId);
        Dictionary<string, Piece> piecesPerPosition = pieces.ToDictionary(_ => _.Position);

        Piece? myPiece = pieces.FirstOrDefault(
            (p) => p.Column == dto.FromColumn && p.Row == dto.FromRow
        );

        if (myPiece == null || myPiece.Color != my.Color)
        {
            return;
        }

        Piece? pieceAtTarget = pieces.FirstOrDefault(
            (p) => p.Column == dto.ToColumn && p.Row == dto.ToRow
        );

        bool hasPieceAtTarget = pieceAtTarget == null;
        bool hasOpponentPieceAtTarget = pieceAtTarget?.IsOponents(myPiece) ?? false;
        PieceColorEnum opponentsColor =
            my.Color == PieceColorEnum.WHITE ? PieceColorEnum.BLACK : PieceColorEnum.WHITE;

        if (myPiece.Value == PieceEnum.PAWN)
        {
            var initialRow = myPiece.IsWhite ? 1 : 6;

            // TODO: Buscar última jogada do adversário

            // TODO: Fazer o inverso para as pecas pretas
            if (myPiece.IsWhite)
            {
                List<string> availablePositions = [];

                // Checar posicoes em linha reta
                int positionsToCheckQuantity = myPiece.Row == initialRow ? 2 : 1;

                for (int i = 1; i <= positionsToCheckQuantity; i++)
                {
                    var positionToCheck = ToPosition(myPiece.Row + i, myPiece.Column);

                    if (piecesPerPosition.TryGetValue(positionToCheck, out _))
                    {
                        break;
                    }

                    availablePositions.Add(positionToCheck);
                }

                // Checar captura
                for (int i = -1; i < 2; i += 2)
                {
                    var columnToCheck = myPiece.Column + i;

                    // Não posso pular pra fora do tabuleiro
                    if (columnToCheck + i > 7 || columnToCheck + i < 0)
                    {
                        continue;
                    }

                    var positionToCheck = ToPosition(myPiece.Row + 1, columnToCheck);

                    if (piecesPerPosition.TryGetValue(positionToCheck, out Piece? pieceAtPosition))
                    {
                        // Não posso capturar minha propria peça
                        if (!pieceAtPosition.IsOponents(myPiece))
                        {
                            continue;
                        }

                        availablePositions.Add(positionToCheck);
                    }
                    // else if()
                    // {
                    //     // en passant
                    // }
                }
            }

            // Verificar movimento inicial: pode avançar 1 ou 2 casas em linha reta
            // Verificar

            // pieces.Where(p => p.Row == dto.ToRow && p.Column == dto.ToColumn);
        }

        // TODO: replace rule verification
        // if(dto.ToColumn >= 0 && dto.ToRow) {

        // }

        // se é movimento natural da peca
        // se é nao possui uma peca de mesma cor no lugar de destino ou no caminho
        // se



        // Console.WriteLine(JsonSerializer.Serialize(matchMakingResponse));
        Console.WriteLine(JsonSerializer.Serialize(dto));
    }

    public string ToPosition(int row, int column)
    {
        return $"{row}{column}";
    }
}
