using ChessAPI.Data;
using ChessAPI.Enums;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Repositories;

public class PieceRepository(AppDbContext context, KingStateRepository kingStateRepository)
    : BaseRepository<Piece>(context)
{
    public List<Piece> SetInitialBoard(Match match, AppDbContext? currentContext)
    {
        List<Piece> pieces = [];

        for (int i = 0; i < 16; i++)
        {
            bool isWhite = i < 8;
            int column = i < 8 ? i : i - 8;
            BoardSideEnum side = column < 4 ? BoardSideEnum.QUEEN : BoardSideEnum.KING;

            pieces.Add(
                new()
                {
                    Value = PieceEnum.PAWN,
                    Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                    Column = column,
                    Row = isWhite ? 1 : 6,
                    WasCaptured = false,
                    Match = match,
                    InitialBoardSide = side,
                }
            );
        }

        PieceEnum[] piecesValues = [PieceEnum.ROOK, PieceEnum.KNIGHT, PieceEnum.BISHOP];

        for (int j = 0; j < piecesValues.Length; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                bool isWhite = i < 2;
                int column = i % 2 == 0 ? j : (7 - j);
                BoardSideEnum side = column < 4 ? BoardSideEnum.QUEEN : BoardSideEnum.KING;

                pieces.Add(
                    new()
                    {
                        Value = piecesValues[j],
                        Column = column,
                        Row = isWhite ? 0 : 7,
                        Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                        WasCaptured = false,
                        Match = match,
                        InitialBoardSide = side,
                    }
                );
            }
        }

        List<Piece> kings = [];
        for (int i = 0; i < 4; i++)
        {
            bool isWhite = i < 2;
            bool isEven = i % 2 == 0;
            int column = isEven ? 3 : 4;

            Piece piece = new()
            {
                Value = isEven ? PieceEnum.QUEEN : PieceEnum.KING,
                Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                Column = column,
                Row = isWhite ? 0 : 7,
                WasCaptured = false,
                Match = match,
                InitialBoardSide = isEven ? BoardSideEnum.QUEEN : BoardSideEnum.KING,
            };

            pieces.Add(piece);

            if (!isEven)
            {
                kings.Add(piece);
            }
        }

        var _context = currentContext ?? Context;
        var dbSet = GetDbSet(_context);

        dbSet.AddRange(pieces);
        kingStateRepository.AddKingsStates(kings, _context);

        return pieces;
    }

    public async Task<Piece> GetPieceByPositionAsync(
        int matchId,
        int row,
        int column,
        AppDbContext? currentContext
    )
    {
        var query = GetQueryBuilder(currentContext ?? Context);

        Piece piece =
            await query.FirstOrDefaultAsync(_ =>
                _.Match.Id == matchId && _.Row == row && _.Column == column && !_.WasCaptured
            ) ?? throw new Exception("Not Found piece");

        return piece;
    }

    public async Task<List<Piece>> GetMatchActivePieces(int matchId, AppDbContext? currentContext)
    {
        var query = GetQueryBuilder(currentContext ?? Context);

        return await query.Where(_ => _.Match.Id == matchId && !_.WasCaptured).ToListAsync();
    }

    public async Task<Dictionary<string, Piece>> GetMatchActivePiecesPerPosition(
        int matchId,
        AppDbContext? currentContext
    )
    {
        var pieces = await GetMatchActivePieces(matchId, currentContext);

        return pieces.ToDictionary(_ => _.Position);
    }
}
