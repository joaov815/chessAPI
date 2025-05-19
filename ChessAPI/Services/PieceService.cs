using ChessAPI.Data;
using ChessAPI.Enums;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class PieceService
{
    public PieceService(AppDbContext context, KingStateService kingStateService)
    {
        Context = context;
        _dbSet = context.Set<Piece>();
        QueryBuilder = _dbSet.AsQueryable();
        _kingStateService = kingStateService;
    }

    protected readonly DbSet<Piece> _dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<Piece> QueryBuilder { get; set; }
    public KingStateService _kingStateService { get; set; }

    public List<Piece> SetInitialBoard(Match match)
    {
        List<Piece> pieces = [];

        for (int i = 0; i < 16; i++)
        {
            bool isWhite = i < 8;
            int column = i < 8 ? i : i - 8;

            pieces.Add(
                new()
                {
                    Value = PieceEnum.PAWN,
                    Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                    Column = column,
                    Row = isWhite ? 1 : 6,
                    WasCaptured = false,
                    Match = match,
                }
            );
        }

        PieceEnum[] piecesValues = [PieceEnum.ROOK, PieceEnum.KNIGHT, PieceEnum.BISHOP];

        for (int j = 0; j < piecesValues.Length; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                bool isWhite = i < 2;

                pieces.Add(
                    new()
                    {
                        Value = piecesValues[j],
                        Column = i % 2 == 0 ? j : (7 - j),
                        Row = isWhite ? 0 : 7,
                        Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                        WasCaptured = false,
                        Match = match,
                    }
                );
            }
        }

        List<Piece> kings = [];
        for (int i = 0; i < 4; i++)
        {
            bool isWhite = i < 2;
            bool isEven = i % 2 == 0;

            Piece piece = new()
            {
                Value = isEven ? PieceEnum.QUEEN : PieceEnum.KING,
                Color = isWhite ? PieceColorEnum.WHITE : PieceColorEnum.BLACK,
                Column = isEven ? 3 : 4,
                Row = isWhite ? 0 : 7,
                WasCaptured = false,
                Match = match,
            };

            pieces.Add(piece);

            if (!isEven)
            {
                kings.Add(piece);
            }
        }

        _dbSet.AddRange(pieces);
        _kingStateService.AddKingsStates(kings);

        return pieces;
    }

    public async Task<Piece> GetPieceByPositionAsync(int matchId, int row, int column)
    {
        Piece piece =
            await QueryBuilder.FirstOrDefaultAsync(_ =>
                _.Match.Id == matchId && _.Row == row && _.Column == column && !_.WasCaptured
            ) ?? throw new Exception("Not Found piece");

        return piece;
    }

    public async Task<List<Piece>> GetMatchActivePieces(int matchId)
    {
        return await QueryBuilder.Where(_ => _.Match.Id == matchId && !_.WasCaptured).ToListAsync();
    }

    public async Task<Dictionary<string, Piece>> GetMatchActivePiecesPerPosition(int matchId)
    {
        var pieces = await GetMatchActivePieces(matchId);

        return pieces.ToDictionary(_ => _.Position);
    }
}
