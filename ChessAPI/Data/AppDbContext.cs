using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Data;

public class AppDbContext(IConfiguration _configuration) : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Match> Matches { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(
                _configuration.GetConnectionString("DefaultConnection"),
                options => options.EnableRetryOnFailure()
            );
        }
    }
}
