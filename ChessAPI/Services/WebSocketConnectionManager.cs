using System.Collections.Concurrent;
using System.Net.WebSockets;
using ChessAPI.Dtos;
using ChessAPI.Enums;
using ChessAPI.Models;
using ChessAPI.Utils;

namespace ChessAPI.Services;

public sealed class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<int, WsClient> _clients = new();

    public void AddClient(WebSocket socket, User user, Match match)
    {
        PieceColorEnum color =
            match.BlackUser?.Id == user.Id ? PieceColorEnum.BLACK : PieceColorEnum.WHITE;

        _clients.TryAdd(
            user.Id,
            new()
            {
                Socket = socket,
                User = user,
                Color = color,
                MatchId = match.Id,
            }
        );
    }

    public WsClient? GetClient(int id)
    {
        _clients.TryGetValue(id, out var client);

        return client;
    }

    public async Task SendMatchClients(int matchId, object message)
    {
        var clients = GetMatchClients(matchId);

        Task[] clientsSend =
        [
            .. clients.Select(client => SocketUtils.SendMessage(client.Socket, message)),
        ];

        await Task.WhenAll(clientsSend);
    }

    public List<WsClient> GetMatchClients(int matchId)
    {
        return [.. _clients.Select(_ => _.Value).Where(_ => _.MatchId == matchId)];
    }

    public IEnumerable<WsClient> GetAllClients() => _clients.Values;

    public async Task HealthCheckAllAsync()
    {
        Console.WriteLine("----HealthCheckAllAsync-----");
        Console.WriteLine(_clients.Count);

        List<int> deadSockets = [];

        foreach (var clientKvp in _clients)
        {
            var socket = clientKvp.Value.Socket;

            if (socket.State != WebSocketState.Open)
            {
                deadSockets.Add(clientKvp.Key);
                continue;
            }

            try
            {
                await SocketUtils.SendMessage(
                    socket,
                    new Dictionary<string, WsMessageTypeResponseEnum>
                    {
                        { "type", WsMessageTypeResponseEnum.PING },
                    }
                );
            }
            catch
            {
                deadSockets.Add(clientKvp.Key);
            }
        }

        foreach (var deadSocket in deadSockets)
        {
            _clients.TryRemove(deadSocket, out var _);
        }
    }
}
