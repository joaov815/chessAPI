namespace ChessAPI.Enums;

public enum WsMessageTypeResponseEnum
{
    PING,
    MATCH_STARTED,
    RECONNECTED,
    MOVE,
    AVAILABLE_POSITIONS,
    INVALID_MOVE,
    INVALID,
}
