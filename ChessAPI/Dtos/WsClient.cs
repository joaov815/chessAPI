using System.Net.WebSockets;
using ChessAPI.Models;

namespace ChessAPI.Dtos;

public class WsClient
{
    public required User User { get; set; }
    public Match? Match { get; set; }
    public required WebSocket Socket { get; set; }
}
