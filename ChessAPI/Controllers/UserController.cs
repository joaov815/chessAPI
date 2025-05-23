using ChessAPI.Dtos;
using ChessAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ChessAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UserController(UserRepository userRepository) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        return Ok(await userRepository.CreateIfNotExistsAsync(dto));
    }
}
