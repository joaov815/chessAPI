using ChessAPI.Data;
using ChessAPI.Dtos;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Repositories;

public class UserRepository(AppDbContext context) : BaseRepository<User>(context)
{
    public async Task<User> CreateIfNotExistsAsync(
        CreateUserDto dto,
        AppDbContext? currentContext = null
    )
    {
        var _context = currentContext ?? Context;
        var _dbSet = GetDbSet(_context);

        User? user = await GetByUsername(dto.Username, _context);

        if (user is not null)
        {
            return user;
        }

        user = new() { Username = dto.Username };

        _dbSet.Add(user);

        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User?> GetByUsername(string username, AppDbContext? currentContext)
    {
        return await GetQueryBuilder(currentContext ?? Context)
            .FirstOrDefaultAsync(u => u.Username == username);
    }
}
