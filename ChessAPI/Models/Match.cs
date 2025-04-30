using System.ComponentModel.DataAnnotations;
using ChessAPI.Enums;

namespace ChessAPI.Models;

public class Match
{
    [Key]
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public required ushort SecondsDuration { get; set; }
    public required User WhiteUser { get; set; }
    public required User BlackUser { get; set; }
    public required MatchStatusEnum MatchStatusEnum { get; set; }
}
