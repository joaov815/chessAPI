using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChessAPI.Dtos;
using ChessAPI.Models;
using ChessAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessAPI.Controllers;

[ApiController]
[Route("/ws")]
public sealed class GameController(
    WebSocketConnectionManager manager,
    MatchService matchService,
    UserService userService
) : ControllerBase
{
    private readonly WebSocketConnectionManager _manager = manager;

    [HttpGet]
    public async Task<IActionResult> Connect()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            return BadRequest("WebSocket connection expected.");

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        await Listen(socket);

        // _manager.Remove(socket);

        return new EmptyResult();
    }

    private async Task Listen(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None
            );

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closed by client",
                    CancellationToken.None
                );
                break;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            JsonSerializerOptions jsonOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            WsMessageDto dto = JsonSerializer.Deserialize<WsMessageDto>(message, jsonOptions)!;

            // Separar
            if (dto.Type == "matchMaking")
            {
                User user =
                    await userService.CreateAsync(new() { Username = dto.Username })
                    ?? throw new InvalidOperationException("User not found");

                _manager.AddClient(webSocket, user);

                await matchService.OnUserConnected(user);

                Console.WriteLine($"{user.Username} no matchmaking....");
            }

            var responseJson = JsonSerializer.Serialize(message, jsonOptions);

            var response = Encoding.UTF8.GetBytes("Echo: " + responseJson);

            await webSocket.SendAsync(
                new ArraySegment<byte>(response),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}
