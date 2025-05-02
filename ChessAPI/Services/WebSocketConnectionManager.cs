using System.Collections.Concurrent;
using System.Net.WebSockets;
using ChessAPI.Dtos;
using ChessAPI.Models;

namespace ChessAPI.Services;

public sealed class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WsClient> _clients = new();

    public string AddClient(WebSocket socket, User user)
    {
        var id = Guid.NewGuid().ToString();

        _clients.TryAdd(id, new WsClient { Socket = socket, User = user });

        return id;
    }

    public WsClient? GetClient(string id)
    {
        _clients.TryGetValue(id, out var client);

        return client;
    }

    public IEnumerable<WsClient> GetAllClients() => _clients.Values;
}
