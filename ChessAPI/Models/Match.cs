using System.ComponentModel.DataAnnotations;
using ChessAPI.Enums;

namespace ChessAPI.Models;

public class Match
{
    [Key]
    public int Id { get; set; }
    public DateTime? StartedAt { get; set; }
    public required ushort SecondsDuration { get; set; }
    public User? WhiteUser { get; set; }
    public User? BlackUser { get; set; }
    public required MatchStatusEnum Status { get; set; }

    public void SetSecondPlayer(User user)
    {
        if (WhiteUser is null)
        {
            WhiteUser = user;
        }
        else
        {
            BlackUser = user;
        }
    }
}
