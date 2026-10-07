using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseHealthController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseHealthController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        await using var command = _dataSource.CreateCommand("SELECT 1");
        var result = await command.ExecuteScalarAsync();

        return Ok(new
        {
            status = "ok",
            database = "Supabase",
            connection = result?.ToString() == "1" ? "conectado" : "erro"
        });
    }
}