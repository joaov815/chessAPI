using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChessAPI.Dtos;
using ChessAPI.Dtos.Response;
using ChessAPI.Enums;
using ChessAPI.Services;
using ChessAPI.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ChessAPI.Controllers;

[ApiController]
[Route("/ws")]
public sealed class GameController : ControllerBase
{
    public GameController(WebSocketConnectionManager manager, MatchService matchService)
    {
        _manager = manager;
        _matchService = matchService;

        _ = CheckClients();
    }

    private readonly WebSocketConnectionManager _manager;
    private readonly MatchService _matchService;

    private async Task CheckClients()
    {
        while (true)
        {
            await _manager.HealthCheckAllAsync();
            await Task.Delay(5_000);
        }
    }

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
        MatchMakingResponseDto? matchMakingResponse = null;

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

            var dto = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                message,
                jsonOptions
            )!;

            // Endpoints
            if (dto.TryGetValue("type", out JsonElement value))
            {
                if (value.GetInt32() == (int)WsMessageTypeEnum.MATCHMAKING)
                {
                    matchMakingResponse = await _matchService.OnMatchMaking(
                        webSocket,
                        JsonSerializer.Deserialize<WsMessageDto>(message, jsonOptions)!
                    );
                }
                else if (matchMakingResponse?.MatchId != null)
                {
                    if (value.GetInt32() == (int)WsMessageTypeEnum.MOVE)
                    {
                        await _matchService.OnMove(
                            webSocket,
                            matchMakingResponse,
                            JsonSerializer.Deserialize<WsMovePieceDto>(message, jsonOptions)!
                        );
                    }
                    else if (
                        value.GetInt32() == (int)WsMessageTypeEnum.GET_PIECE_AVAILABLE_POSITIONS
                        && matchMakingResponse?.MatchId != null
                    )
                    {
                        await _matchService.GetPieceAvailablePositions(
                            webSocket,
                            matchMakingResponse,
                            JsonSerializer.Deserialize<GetPiecePositionsDto>(message, jsonOptions)!
                        );
                    }
                }
            }

            await SocketUtils.SendMessage(webSocket, message);
        }
    }
}
