using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using ChessAPI.Dtos;
using ChessAPI.Models;

namespace ChessAPI.Services;

public sealed class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WsClient> _clients = new();

    public string AddClient(WebSocket socket, User user)
    {
        var id = Guid.NewGuid().ToString();

        _clients.TryAdd(id, new() { Socket = socket, User = user });

        return id;
    }

    public WsClient? GetClient(string id)
    {
        _clients.TryGetValue(id, out var client);

        return client;
    }

    public IEnumerable<WsClient> GetAllClients() => _clients.Values;

    public async Task HealthCheckAllAsync()
    {
        List<string> deadSockets = [];

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
                var pingMessage = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
                await socket.SendAsync(
                    new ArraySegment<byte>(pingMessage),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
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
