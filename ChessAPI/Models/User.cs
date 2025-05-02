using System.ComponentModel.DataAnnotations;

namespace ChessAPI.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    public required string Username { get; set; }
}
