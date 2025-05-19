using System.Net.WebSockets;
using ChessAPI.Enums;
using ChessAPI.Models;

namespace ChessAPI.Dtos;

public class WsClient
{
    public required User User { get; set; }
    public int? MatchId { get; set; }
    public required PieceColorEnum Color { get; set; }
    public required WebSocket Socket { get; set; }
}
