using System.Net.WebSockets;
using ChessAPI.Data;
using ChessAPI.Dtos;
using ChessAPI.Dtos.Response;
using ChessAPI.Enums;
using ChessAPI.Models;
using ChessAPI.Repositories;
using ChessAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class MatchService(
    IServiceProvider serviceProvider,
    MatchRepository matchRepository,
    UserRepository userRepository,
    PieceRepository pieceRepository,
    MatchPieceHistoryRepository matchPieceHistoryRepository,
    WebSocketConnectionManager manager,
    KingStateRepository kingStateRepository
)
{
    public async Task<MatchMakingResponseDto> OnMatchMaking(WebSocket webSocket, WsMessageDto dto)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        User user = await userRepository.CreateIfNotExistsAsync(
            new() { Username = dto.Username },
            context
        );

        var match = await OnUserConnected(context, webSocket, user);

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

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Match match = await matchRepository.GetOngoingByIdAsync(my.MatchId, context);

        MatchPieceHistory? lastMove = await matchPieceHistoryRepository.GetMatchLastPieceHistory(
            my.MatchId,
            context
        );

        var piecesPerPosition = await pieceRepository.GetMatchActivePiecesPerPosition(
            my.MatchId,
            context
        );

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

        List<string> availablePositions = await GetAvailablePositions(
            myPiece,
            piecesPerPosition,
            lastMove,
            context
        );

        if (availablePositions.Contains(dto.ToPosition))
        {
            await Move(match, myPiece, dto, piecesPerPosition, context);
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
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Piece piece = await pieceRepository.GetPieceByPositionAsync(
            my.MatchId,
            dto.Row,
            dto.Column,
            context
        );
        object response;
        MatchPieceHistory? lastMove = await matchPieceHistoryRepository.GetMatchLastPieceHistory(
            my.MatchId,
            context
        );

        if (
            piece.Color == my.Color
            && (
                (lastMove is null && my.Color == PieceColorEnum.WHITE)
                || (lastMove != null && lastMove.Piece.Color != my.Color)
            )
        )
        {
            var piecesPerPosition = await pieceRepository.GetMatchActivePiecesPerPosition(
                my.MatchId,
                context
            );

            response = new AvailablePositionsDto
            {
                Positions = await GetAvailablePositions(
                    piece,
                    piecesPerPosition,
                    lastMove,
                    context
                ),
            };
        }
        else
        {
            response = new BaseResponseDto { Type = WsMessageTypeResponseEnum.INVALID };
        }

        await SocketUtils.SendMessage(mySocket, response);
    }

    private async Task<Match> OnUserConnected(AppDbContext context, WebSocket webSocket, User user)
    {
        Match? myUnfinishedMatch = await matchRepository.GetMyUnfinishedMatch(user, context);

        if (myUnfinishedMatch != null)
        {
            List<Piece> pieces = await pieceRepository.GetMatchActivePieces(
                myUnfinishedMatch.Id,
                context
            );

            var myColor =
                myUnfinishedMatch.BlackUser?.Id == user.Id
                    ? PieceColorEnum.BLACK
                    : PieceColorEnum.WHITE;

            manager.AddClient(webSocket, user, myUnfinishedMatch);

            await SocketUtils.SendMessage(
                webSocket,
                new MatchReconnectedDto
                {
                    Color = myColor,
                    Pieces = pieces,
                    BlackUsername = myUnfinishedMatch.BlackUser?.Username,
                    WhiteUsername = myUnfinishedMatch.WhiteUser?.Username,
                }
            );

            return myUnfinishedMatch;
        }

        Match? match = await matchRepository.GetMatchMakingMatch(user, context);

        if (match is null)
        {
            match = await matchRepository.CreateAsync(user, context);
            manager.AddClient(webSocket, user, match);
        }
        else
        {
            manager.AddClient(webSocket, user, match);
            await StartMatch(user, match, context);
        }

        return match!;
    }

    private async Task StartMatch(User user, Match match, AppDbContext context)
    {
        match.SetSecondPlayer(user);
        match.StartedAt = DateTime.UtcNow;
        match.Status = MatchStatusEnum.ONGOING;

        pieceRepository.SetInitialBoard(match, context);

        await context.SaveChangesAsync();

        List<WsClient> clients = manager.GetMatchClients(match.Id);

        await Task.WhenAll(
            clients.Select(c =>
                SocketUtils.SendMessage(
                    c.Socket,
                    new MatchStartedResponseDto
                    {
                        Type = WsMessageTypeResponseEnum.MATCH_STARTED,
                        Color = c.Color,
                        BlackUsername = match.BlackUser!.Username,
                        WhiteUsername = match.WhiteUser!.Username,
                    }
                )
            )
        );
    }

    private async Task<List<string>> GetAvailablePositions(
        Piece piece,
        Dictionary<string, Piece> piecesPerPosition,
        MatchPieceHistory? lastMove,
        AppDbContext context
    )
    {
        return piece.Value switch
        {
            PieceEnum.PAWN => GetPawnAvailablePositions(piece, piecesPerPosition, lastMove),
            PieceEnum.BISHOP or PieceEnum.ROOK or PieceEnum.QUEEN => GetBRQAvailablePositions(
                piece,
                piecesPerPosition
            ),
            PieceEnum.KNIGHT => GetKnightAvailablePositions(piece, piecesPerPosition),
            PieceEnum.KING => await GetKingAvailablePositions(piece.Id, piecesPerPosition, context),
            _ => throw new Exception("INVALID PIECE"),
        };
    }

    public async Task<List<string>> GetKingAvailablePositions(
        int pieceId,
        Dictionary<string, Piece> piecesPerPosition,
        AppDbContext currentContext
    )
    {
        var king = await kingStateRepository.GetByPieceId(pieceId, currentContext);

        // TODO: Add Castle move

        return
        [
            .. king.NonEnemyPositions.Where(position =>
                !piecesPerPosition.TryGetValue(position, out _)
            ),
        ];
    }

    public async Task Move(
        Match match,
        Piece myPiece,
        WsMovePieceDto dto,
        Dictionary<string, Piece> piecesPerPosition,
        AppDbContext context
    )
    {
        myPiece.Column = dto.ToColumn;
        myPiece.Row = dto.ToRow;

        string possibleCapturePosition = myPiece.EnPassantCapturePosition ?? dto.ToPosition;

        piecesPerPosition.TryGetValue(possibleCapturePosition, out Piece? pieceAtTarget);

        if (pieceAtTarget != null)
        {
            pieceAtTarget.WasCaptured = true;
        }

        ushort round = (ushort)(myPiece.IsWhite ? match.Rounds + 1 : match.Rounds);

        var history = matchPieceHistoryRepository.Save(
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
            },
            context
        );

        match.Rounds = round;

        KingState opponentKing = await kingStateRepository.GetOpponentKing(
            match.Id,
            myPiece.Color,
            context
        );
        List<string> newAvailablePositions = await GetAvailablePositions(
            myPiece,
            piecesPerPosition,
            history,
            context
        );

        opponentKing.OpponentPositionsAround =
        [
            .. newAvailablePositions.Where(opponentKing.PositionsAround.Contains),
        ];

        await context.SaveChangesAsync();

        MoveResponseDto response = new()
        {
            Type = WsMessageTypeResponseEnum.MOVE,
            History = history,
            CapturedEnPassantPawn = myPiece.EnPassantCapturePosition,
        };

        await manager.SendMatchClients(match.Id, response);
    }

    private static List<string> GetPawnAvailablePositions(
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
                myPiece.EnPassantCapturePosition = enPassantPos;
            }
        }

        return availablePositions;
    }

    private static List<string> GetKnightAvailablePositions(
        Piece myPiece,
        Dictionary<string, Piece> piecesPerPosition
    )
    {
        List<string> availablePositions = [];

        int[][] directions =
        [
            [1, -2],
            [2, -1],
            [2, 1],
            [1, 2],
            [-1, 2],
            [-2, 1],
            [-2, -1],
            [-1, -2],
        ];

        foreach (var direction in directions)
        {
            int r = myPiece.Row + direction[0];
            int c = myPiece.Column + direction[1];
            string position = $"{r}{c}";

            if (
                r < 0
                || r > 8
                || c < 0 && c > 8
                || (
                    piecesPerPosition.TryGetValue(position, out Piece? pieceAtPosition)
                    && !pieceAtPosition.IsOponents(myPiece)
                )
            )
            {
                continue;
            }

            availablePositions.Add(position);
        }

        return availablePositions;
    }

    private static List<string> GetBRQAvailablePositions(
        Piece myPiece,
        Dictionary<string, Piece> piecesPerPosition
    )
    {
        List<string> availablePositions = [];

        int[][] bishopDirections =
        [
            [-1, -1],
            [1, -1],
            [-1, 1],
            [1, 1],
        ];
        int[][] rookDirections =
        [
            [-1, 0],
            [1, 0],
            [0, -1],
            [0, 1],
        ];

        Dictionary<PieceEnum, int[][]> directionsPerPiece = new()
        {
            { PieceEnum.BISHOP, bishopDirections },
            { PieceEnum.ROOK, rookDirections },
            { PieceEnum.QUEEN, [.. bishopDirections, .. rookDirections] },
        };

        int[][] directions = directionsPerPiece[myPiece.Value];

        for (int i = 0; i < directions.Length; i++)
        {
            int r = myPiece.Row + directions[i][0];
            int c = myPiece.Column + directions[i][1];

            while (r >= 0 && r < 8 && c >= 0 && c < 8)
            {
                string position = $"{r}{c}";

                if (piecesPerPosition.TryGetValue(position, out Piece? pieceAtPosition))
                {
                    if (pieceAtPosition.IsOponents(myPiece))
                    {
                        availablePositions.Add(position);
                    }
                    break;
                }

                availablePositions.Add(position);

                r += directions[i][0];
                c += directions[i][1];
            }
        }

        return availablePositions;
    }

    private static string ToPosition(int row, int column)
    {
        return $"{row}{column}";
    }
}
