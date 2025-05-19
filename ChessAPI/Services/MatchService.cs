using System.Net.WebSockets;
using System.Text.Json;
using ChessAPI.Data;
using ChessAPI.Dtos;
using ChessAPI.Dtos.Response;
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
            _manager.SetClientMatchId(user.Id, myUnfinishedMatch.Id);
            List<Piece> pieces = await _pieceService.GetMatchActivePieces(myUnfinishedMatch.Id);

            await SocketUtils.SendMessage(
                webSocket,
                new MatchReconnectedDto
                {
                    Color =
                        myUnfinishedMatch.BlackUser?.Id == user.Id
                            ? PieceColorEnum.BLACK
                            : PieceColorEnum.WHITE,
                    Pieces = pieces,
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

        _manager.SetClientMatchId(user.Id, match.Id);

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
            Color = match.BlackUser?.Id == user.Id ? PieceColorEnum.BLACK : PieceColorEnum.WHITE,
        };
    }

    public async Task OnMove(WebSocket mySocket, MatchMakingResponseDto my, WsMovePieceDto dto)
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

        var piecesPerPosition = await _pieceService.GetMatchActivePiecesPerPosition(my.MatchId);

        Piece? myPiece = piecesPerPosition.FirstOrDefault((p) => p.Key == dto.FromPosition).Value;

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

        List<string> availablePositions = GetAvailablePositions(
            myPiece,
            piecesPerPosition,
            lastMove
        );

        if (availablePositions.Contains(dto.ToPosition))
        {
            await Move(match, myPiece, dto, piecesPerPosition);
        }
        else
        {
            await SocketUtils.SendMessage(
                mySocket,
                new BaseResponseDto { Type = WsMessageTypeResponseEnum.INVALID_MOVE }
            );
        }
    }

    public async Task GetPieceAvailablePositions(
        WebSocket mySocket,
        MatchMakingResponseDto my,
        GetPiecePositionsDto dto
    )
    {
        Piece piece = await _pieceService.GetPieceByPositionAsync(my.MatchId, dto.Row, dto.Column);
        object response;
        MatchPieceHistory? lastMove = await _matchPieceHistoryService.GetMatchLastPieceHistory(
            my.MatchId
        );

        if (
            piece.Color == my.Color
            && (
                (lastMove is null && my.Color == PieceColorEnum.WHITE)
                || (lastMove != null && lastMove.Piece.Color != my.Color)
            )
        )
        {
            var piecesPerPosition = await _pieceService.GetMatchActivePiecesPerPosition(my.MatchId);

            response = new AvailablePositionsDto
            {
                Positions = GetAvailablePositions(piece, piecesPerPosition, lastMove),
            };
        }
        else
        {
            response = new BaseResponseDto { Type = WsMessageTypeResponseEnum.INVALID };
        }

        await SocketUtils.SendMessage(mySocket, response);
    }

    private List<string> GetAvailablePositions(
        Piece piece,
        Dictionary<string, Piece> piecesPerPosition,
        MatchPieceHistory? lastMove
    )
    {
        List<string> availablePositions = [];

        if (piece.Value == PieceEnum.PAWN)
        {
            availablePositions = GetPawnAvailablePositions(piece, piecesPerPosition, lastMove);
        }
        else if (piece.Value == PieceEnum.BISHOP)
        {
            // TODO:
        }
        else if (piece.Value == PieceEnum.KNIGHT)
        {
            // TODO:
        }
        else if (piece.Value == PieceEnum.ROOK)
        {
            // TODO:
        }
        else if (piece.Value == PieceEnum.QUEEN)
        {
            // TODO:
        }
        else if (piece.Value == PieceEnum.KING)
        {
            // TODO:
        }

        return availablePositions;
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

        string possibleCapturePosition = myPiece.OnPassantCapturePosition ?? dto.ToPosition;

        piecesPerPosition.TryGetValue(possibleCapturePosition, out Piece? pieceAtTarget);

        if (pieceAtTarget != null)
        {
            pieceAtTarget.WasCaptured = true;
        }

        ushort round = (ushort)(myPiece.IsWhite ? match.Rounds + 1 : match.Rounds);

        var history = _matchPieceHistoryService.Save(
            new()
            {
                Piece = myPiece,
                CreatedAt = DateTime.UtcNow,
                CurrentColumn = dto.ToColumn,
                CurrentRow = dto.ToRow,
                PreviousColumn = dto.FromColumn,
                PreviousRow = dto.FromRow,
                Match = match,
                Round = round,
            }
        );

        match.Rounds = round;

        await Context.SaveChangesAsync();

        MoveResponseDto response = new()
        {
            Type = WsMessageTypeResponseEnum.MOVE,
            History = history,
            CapturedEnPassantPawn = myPiece.OnPassantCapturePosition,
        };

        await _manager.SendMatchClients(match.Id, response);
    }

    public List<string> GetPawnAvailablePositions(
        Piece myPiece,
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
            if (columnToCheck > 7 || columnToCheck < 0)
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
                && Math.Abs(lastMove.CurrentRow - lastMove.PreviousRow) == 2
            )
            {
                // en passant
                availablePositions.Add(positionToCheck);
                var enPassantPos = ToPosition(
                    rowToCheck + (myPiece.IsWhite ? -1 : 1),
                    columnToCheck
                );
                myPiece.OnPassantCapturePosition = enPassantPos;
            }
        }

        return availablePositions;
    }

    public string ToPosition(int row, int column)
    {
        return $"{row}{column}";
    }
}
