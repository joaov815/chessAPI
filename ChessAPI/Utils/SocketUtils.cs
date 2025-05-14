using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ChessAPI.Utils;

public class SocketUtils
{
    public static async Task SendMessage(WebSocket webSocket, object message)
    {
        var responseJson = JsonSerializer.Serialize(
            message,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
        var response = Encoding.UTF8.GetBytes(responseJson);

        await webSocket.SendAsync(
            new ArraySegment<byte>(response),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }
}
