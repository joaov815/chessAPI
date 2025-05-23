using ChessAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Repositories;

public class BaseRepository<T>
    where T : class
{
    public BaseRepository(AppDbContext context)
    {
        Context = context;
        dbSet = context.Set<T>();
        QueryBuilder = dbSet.AsQueryable();
    }

    protected readonly DbSet<T> dbSet;
    public AppDbContext Context { get; set; }
    public IQueryable<T> QueryBuilder { get; set; }

    public static DbSet<T> GetDbSet(AppDbContext context)
    {
        return context.Set<T>();
    }

    public static IQueryable<T> GetQueryBuilder(AppDbContext context)
    {
        DbSet<T> dbSet = GetDbSet(context);

        return dbSet.AsQueryable();
    }
}
