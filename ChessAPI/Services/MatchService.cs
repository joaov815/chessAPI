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
            await StartMatch(webSocket, user, match, context);
        }

        return match!;
    }

    private async Task StartMatch(WebSocket webSocket, User user, Match match, AppDbContext context)
    {
        match.SetSecondPlayer(user);
        match.StartedAt = DateTime.UtcNow;
        match.Status = MatchStatusEnum.ONGOING;

        pieceRepository.SetInitialBoard(match, context);

        await context.SaveChangesAsync();

        manager.AddClient(webSocket, user, match);

        List<WsClient> clients = manager.GetMatchClients(match.Id);

        // TODO: Fix start match player 2 wrong color bug

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
        var isWhite = king.Piece.Color == PieceColorEnum.WHITE;

        List<string> availablePositions =
        [
            .. king.PositionsAround.Where(position =>
            {
                bool hasOpponentInfluence = king.OpponentPositionsAround.Contains(position); // Não permite casas controladas por peças adversárias
                bool hasPieceAt = piecesPerPosition.TryGetValue(position, out _); // Não permite casas que possuam peças

                return !hasOpponentInfluence && !hasPieceAt;
            }),
        ];

        List<string> kingCastlingPositions = [];
        List<string> queenCastlingPositions = [];

        for (int i = 1; i <= 2; i++)
        {
            var pos = $"{king.Piece.Row}{king.Piece.Column + (isWhite ? i : i)}";

            if (!piecesPerPosition.TryGetValue(pos, out Piece? _))
            {
                kingCastlingPositions.Add(pos);
            }
        }
        for (int i = 1; i <= 3; i++)
        {
            var pos = $"{king.Piece.Row}{king.Piece.Column + (isWhite ? -i : -i)}";

            if (!piecesPerPosition.TryGetValue(pos, out Piece? _))
            {
                queenCastlingPositions.Add(pos);
            }
        }

        bool canKingCastle = kingCastlingPositions.Count == 2;
        bool canQueenCastle = queenCastlingPositions.Count == 3;

        if (canKingCastle || canQueenCastle)
        {
            var history = await matchPieceHistoryRepository.GetMatchHistory(
                king.Piece.MatchId,
                currentContext
            );

            if (history.Any(_ => _.Piece.Id == pieceId))
            {
                return availablePositions;
            }

            canKingCastle &= !history.Any(_ =>
                _.Piece.Color == king.Piece.Color
                && _.Piece.Value == PieceEnum.ROOK
                && _.Piece.InitialBoardSide == BoardSideEnum.KING
            );
            canQueenCastle &= !history.Any(_ =>
                _.Piece.Color == king.Piece.Color
                && _.Piece.Value == PieceEnum.ROOK
                && _.Piece.InitialBoardSide == BoardSideEnum.QUEEN
            );

            if (canKingCastle)
            {
                availablePositions.AddRange(kingCastlingPositions);
            }
            if (canQueenCastle)
            {
                availablePositions.AddRange(queenCastlingPositions[..2]);
            }
        }

        return availablePositions;
    }

    private static void Capture(
        Piece myPiece,
        WsMovePieceDto dto,
        Dictionary<string, Piece> piecesPerPosition
    )
    {
        string possibleCapturePosition = myPiece.EnPassantCapturePosition ?? dto.ToPosition;

        piecesPerPosition.TryGetValue(possibleCapturePosition, out Piece? pieceAtTarget);

        if (pieceAtTarget != null)
        {
            pieceAtTarget.WasCaptured = true;
        }
    }

    private async Task<WsMovePieceDto?> Castle(
        Piece myPiece,
        WsMovePieceDto dto,
        Dictionary<string, Piece> piecesPerPosition,
        AppDbContext context,
        List<string> newAvailablePositions
    )
    {
        WsMovePieceDto? castleRookMove = null;
        int squaresMoved = dto.FromColumn - dto.ToColumn;
        bool isCastleMove = Math.Abs(squaresMoved) == 2;

        if (isCastleMove)
        {
            bool isKCastle = squaresMoved < 0;

            Piece rook = piecesPerPosition
                .FirstOrDefault(_ =>
                    _.Value.Color == myPiece.Color
                    && (
                        isKCastle
                            ? _.Value.InitialBoardSide == BoardSideEnum.KING
                            : _.Value.InitialBoardSide == BoardSideEnum.QUEEN
                    )
                    && _.Value.Value == PieceEnum.ROOK
                )
                .Value;

            int newColumn = isKCastle ? 5 : 3;

            castleRookMove = new()
            {
                FromColumn = rook.Column,
                FromRow = rook.Row,
                ToColumn = newColumn,
                ToRow = rook.Row,
            };

            rook.Column = newColumn;
            context.Update(rook);

            newAvailablePositions.AddRange(
                await GetAvailablePositions(rook, piecesPerPosition, null, context)
            );
        }
        return castleRookMove;
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

        Capture(myPiece, dto, piecesPerPosition);

        ushort round = (ushort)(myPiece.IsWhite ? match.Rounds + 1 : match.Rounds);
        match.Rounds = round;

        MatchPieceHistory history = matchPieceHistoryRepository.Save(
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

        MoveResponseDto response = new()
        {
            Type = WsMessageTypeResponseEnum.MOVE,
            History = history,
            CapturedEnPassantPawn = myPiece.EnPassantCapturePosition,
        };

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

        if (myPiece.Value == PieceEnum.KING)
        {
            response.CastleRookMove = await Castle(
                myPiece,
                dto,
                piecesPerPosition,
                context,
                newAvailablePositions
            );

            KingState myKing = await kingStateRepository.GetByPieceId(myPiece.Id, context);
            myKing.PositionsAround = PositionUtils.GetPositionsAround(myPiece);
        }

        opponentKing.OpponentPositionsAround =
        [
            .. newAvailablePositions.Where(opponentKing.PositionsAround.Contains),
        ];

        // Verifica pecas cravadas
        foreach (var pos in opponentKing.OpponentPositionsAround)
        {
            if (piecesPerPosition.TryGetValue(pos, out Piece? _piece))
            {
                _piece.IsPinned = true;
            }
        }

        // Checa Check
        if (newAvailablePositions.Contains(opponentKing.Piece.Position))
        {
            history.ColorOnCheck = opponentKing.Piece.Color;
        }

        await context.SaveChangesAsync();
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
