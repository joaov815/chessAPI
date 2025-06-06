using ChessAPI.Data;
using ChessAPI.Repositories;
using ChessAPI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddScoped<KingStateRepository>();
builder.Services.AddScoped<MatchPieceHistoryRepository>();
builder.Services.AddScoped<MatchRepository>();
builder.Services.AddScoped<PieceRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddSingleton<WebSocketConnectionManager>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.UseWebSockets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.Run();
