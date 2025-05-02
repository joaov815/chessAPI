using ChessAPI.Data;
using ChessAPI.Dtos;
using ChessAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services;

public class UserService
{
    protected readonly DbSet<User> _dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<User> QueryBuilder { get; set; }

    public UserService(AppDbContext context)
    {
        Context = context;
        _dbSet = context.Set<User>();
        QueryBuilder = _dbSet.AsQueryable();
    }

    public async Task<User> CreateAsync(CreateUserDto dto)
    {
        User? user = await GetByUsername(dto.Username);

        if (user is not null)
        {
            return user;
        }

        user = new() { Username = dto.Username };

        _dbSet.Add(user);

        await Context.SaveChangesAsync();

        return user;
    }

    public async Task<User?> GetByUsername(string username)
    {
        return await QueryBuilder.FirstOrDefaultAsync(u => u.Username == username);
    }
}
