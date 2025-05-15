using System.Net.WebSockets;
using ChessAPI.Data;
using ChessAPI.Dtos;
using ChessAPI.Enums;
using ChessAPI.Models;
using ChessAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class MatchService
{
    public MatchService(
        AppDbContext context,
        UserService userService,
        PieceService pieceService,
        MatchPieceHistoryService matchPieceHistoryService,
        WebSocketConnectionManager manager
    )
    {
        Context = context;
        _dbSet = context.Set<Match>();
        QueryBuilder = _dbSet.AsQueryable();

        _userService = userService;
        _pieceService = pieceService;
        _matchPieceHistoryService = matchPieceHistoryService;
        _manager = manager;
    }

    private readonly WebSocketConnectionManager _manager;

    protected readonly DbSet<Match> _dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<Match> QueryBuilder { get; set; }
    private readonly UserService _userService;
    private readonly PieceService _pieceService;
    private readonly MatchPieceHistoryService _matchPieceHistoryService;

    public async Task<Match> OnUserConnected(WebSocket webSocket, User user)
    {
        Match? myUnfinishedMatch = await GetMyUnfinishedMatch(user);

        if (_manager.GetClient(user.Id) is null)
        {
            _manager.AddClient(webSocket, user);
        }
        if (myUnfinishedMatch != null)
        {
            await SocketUtils.SendMessage(
                webSocket,
                new MatchStartedResponseDto
                {
                    Type = WsMessageTypeResponseEnum.RECONNECTED,
                    Color =
                        myUnfinishedMatch.BlackUser?.Id == user.Id
                            ? PieceColorEnum.BLACK
                            : PieceColorEnum.WHITE,
                }
            );

            return myUnfinishedMatch;
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

        WsClient whiteClient = _manager.GetClient(match.WhiteUser!.Id)!;
        WsClient blackClient = _manager.GetClient(match.BlackUser!.Id)!;

        await SocketUtils.SendMessage(
            whiteClient.Socket,
            new MatchStartedResponseDto
            {
                Type = WsMessageTypeResponseEnum.MATCH_STARTED,
                Color = PieceColorEnum.WHITE,
            }
        );
        await SocketUtils.SendMessage(
            blackClient.Socket,
            new MatchStartedResponseDto
            {
                Type = WsMessageTypeResponseEnum.MATCH_STARTED,
                Color = PieceColorEnum.BLACK,
            }
        );

        Console.WriteLine("match Started");
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

    public async Task<Match?> GetMyUnfinishedMatch(User user)
    {
        return await QueryBuilder
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

    public async Task<MatchMakingResponseDto> OnMatchMaking(WebSocket webSocket, WsMessageDto dto)
    {
        User user = await _userService.CreateIfNotExistsAsync(new() { Username = dto.Username });

        var match = await OnUserConnected(webSocket, user);

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

        Match match =
            await QueryBuilder.FirstOrDefaultAsync(
                (_) => _.Id == my.MatchId && _.Status == MatchStatusEnum.ONGOING
            ) ?? throw new Exception("Not found");

        MatchPieceHistory? lastMove = await _matchPieceHistoryService.GetMatchLastPieceHistory(
            my.MatchId
        );

        List<Piece> pieces = await _pieceService.GetMatchActivePiecesAsync(my.MatchId);
        Dictionary<string, Piece> piecesPerPosition = pieces.ToDictionary(_ => _.Position);

        Piece? myPiece = pieces.FirstOrDefault(
            (p) => p.Column == dto.FromColumn && p.Row == dto.FromRow
        );

        // Valida última jogada e garante que a cor da jogada seja diferente da anterior
        if (
            lastMove == null && match.BlackUser!.Id == my.User.Id
            || lastMove?.Piece.Color == my.Color
            || myPiece == null
            || myPiece.Color != my.Color
        )
        {
            return;
        }

        PieceColorEnum opponentsColor =
            my.Color == PieceColorEnum.WHITE ? PieceColorEnum.BLACK : PieceColorEnum.WHITE;

        switch (myPiece.Value)
        {
            case PieceEnum.PAWN:
                await MovePawn(match, myPiece, dto, piecesPerPosition, lastMove);
                break;
            case PieceEnum.BISHOP:
                // MoveBishop
                break;
            case PieceEnum.KNIGHT:
                // MoveKnight
                break;
            case PieceEnum.HOOK:
                // MoveHook
                break;
            case PieceEnum.QUEEN:
                // MoveQueen
                break;
            default:
                // MoveKing
                break;
        }
    }

    public async Task Move(
        Match match,
        Piece myPiece,
        WsMovePieceDto dto,
        Dictionary<string, Piece> piecesPerPosition
    )
    {
        myPiece.Column = dto.ToColumn;
        myPiece.Row = dto.ToRow;

        piecesPerPosition.TryGetValue(dto.ToPosition, out Piece? pieceAtTarget);

        if (pieceAtTarget != null)
        {
            pieceAtTarget.WasCaptured = true;
        }

        ushort round = (ushort)(myPiece.IsWhite ? match.Rounds + 1 : match.Rounds);

        _matchPieceHistoryService.Save(
            new()
            {
                Piece = myPiece,
                CreatedAt = DateTime.Now,
                CurrentColumn = dto.ToColumn,
                CurrentRow = dto.ToRow,
                PreviousColumn = dto.FromColumn,
                PreviousRow = dto.FromColumn,
                Match = match,
                Round = round,
            }
        );

        match.Rounds = round;

        await Context.SaveChangesAsync();
    }

    public async Task MovePawn(
        Match match,
        Piece myPiece,
        WsMovePieceDto dto,
        Dictionary<string, Piece> piecesPerPosition,
        MatchPieceHistory? lastMove
    )
    {
        var initialRow = myPiece.IsWhite ? 1 : 6;

        List<string> availablePositions = [];

        // Checar posicoes em linha reta
        int positionsToCheckQuantity = myPiece.Row == initialRow ? 2 : 1;

        for (int i = 1; i <= positionsToCheckQuantity; i++)
        {
            var rowToCheck = myPiece.Row + (myPiece.IsWhite ? i : -i);
            var positionToCheck = ToPosition(rowToCheck, myPiece.Column);

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

            var rowToCheck = myPiece.Row + (myPiece.IsWhite ? 1 : -1);
            var positionToCheck = ToPosition(rowToCheck, columnToCheck);

            if (piecesPerPosition.TryGetValue(positionToCheck, out Piece? pieceAtPosition))
            {
                // Não posso capturar minha propria peça
                if (!pieceAtPosition.IsOponents(myPiece))
                {
                    continue;
                }

                availablePositions.Add(positionToCheck);
            }
            else if (
                lastMove?.Piece.Value == PieceEnum.PAWN
                && lastMove.CurrentColumn == columnToCheck
                && lastMove.CurrentRow == myPiece.Row
            )
            {
                // en passant
                availablePositions.Add(positionToCheck);
            }
        }

        if (availablePositions.Contains(dto.ToPosition))
        {
            await Move(match, myPiece, dto, piecesPerPosition);
        }
    }

    public string ToPosition(int row, int column)
    {
        return $"{row}{column}";
    }
}
