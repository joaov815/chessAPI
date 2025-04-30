using ChessAPI.Data;
using ChessAPI.Dtos;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class UserService(AppDbContext context)
{
    protected readonly DbSet<User> _dbSet = context.Set<User>();

    public async Task CreateAsync(CreateUserDto dto)
    {
        User user = new() { Username = dto.Username };

        _dbSet.Add(user);

        await context.SaveChangesAsync();
    }
}
