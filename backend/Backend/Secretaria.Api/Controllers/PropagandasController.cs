using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Secretaria.Api.DTOs;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropagandasController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public PropagandasController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        const string sql = """
            SELECT id, titulo, imagem_url, ativa, ordem,
                   created_at, updated_at
            FROM public.propagandas
            ORDER BY ordem ASC, created_at DESC;
            """;

        var lista = new List<object>();

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            lista.Add(new
            {
                id = reader.GetGuid(0),
                titulo = reader.GetString(1),
                imagemUrl = reader.GetString(2),
                ativa = reader.GetBoolean(3),
                ordem = reader.GetInt32(4),
                criadoEm = reader.GetFieldValue<DateTime>(5),
                atualizadoEm = reader.GetFieldValue<DateTime>(6)
            });
        }

        return Ok(lista);
    }

    [HttpGet("ativas")]
    public async Task<IActionResult> ListarAtivas()
    {
        const string sql = """
            SELECT id, titulo, imagem_url, ativa, ordem,
                   created_at, updated_at
            FROM public.propagandas
            WHERE ativa = TRUE
            ORDER BY ordem ASC, created_at DESC;
            """;

        var lista = new List<object>();

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            lista.Add(new
            {
                id = reader.GetGuid(0),
                titulo = reader.GetString(1),
                imagemUrl = reader.GetString(2),
                ativa = reader.GetBoolean(3),
                ordem = reader.GetInt32(4),
                criadoEm = reader.GetFieldValue<DateTime>(5),
                atualizadoEm = reader.GetFieldValue<DateTime>(6)
            });
        }

        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] CriarPropagandaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Titulo))
            return BadRequest(new { erro = "Informe o título." });

        if (string.IsNullOrWhiteSpace(dto.ImagemUrl))
            return BadRequest(new { erro = "Informe a URL da imagem." });

        const string sql = """
            INSERT INTO public.propagandas
                (titulo, imagem_url, ativa, ordem)
            VALUES ($1, $2, $3, $4)
            RETURNING id, titulo, imagem_url, ativa, ordem,
                      created_at, updated_at;
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(dto.Titulo.Trim());
        cmd.Parameters.AddWithValue(dto.ImagemUrl.Trim());
        cmd.Parameters.AddWithValue(dto.Ativa);
        cmd.Parameters.AddWithValue(dto.Ordem);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        var resultado = new
        {
            id = reader.GetGuid(0),
            titulo = reader.GetString(1),
            imagemUrl = reader.GetString(2),
            ativa = reader.GetBoolean(3),
            ordem = reader.GetInt32(4),
            criadoEm = reader.GetFieldValue<DateTime>(5),
            atualizadoEm = reader.GetFieldValue<DateTime>(6)
        };

        return Created($"/api/Propagandas/{resultado.id}", resultado);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPropagandaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Titulo))
            return BadRequest(new { erro = "Informe o título." });

        if (string.IsNullOrWhiteSpace(dto.ImagemUrl))
            return BadRequest(new { erro = "Informe a URL da imagem." });

        const string sql = """
            UPDATE public.propagandas
            SET titulo = $1,
                imagem_url = $2,
                ativa = $3,
                ordem = $4,
                updated_at = NOW()
            WHERE id = $5
            RETURNING id, titulo, imagem_url, ativa, ordem,
                      created_at, updated_at;
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(dto.Titulo.Trim());
        cmd.Parameters.AddWithValue(dto.ImagemUrl.Trim());
        cmd.Parameters.AddWithValue(dto.Ativa);
        cmd.Parameters.AddWithValue(dto.Ordem);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return NotFound(new { erro = "Propaganda não encontrada." });

        return Ok(new
        {
            id = reader.GetGuid(0),
            titulo = reader.GetString(1),
            imagemUrl = reader.GetString(2),
            ativa = reader.GetBoolean(3),
            ordem = reader.GetInt32(4),
            criadoEm = reader.GetFieldValue<DateTime>(5),
            atualizadoEm = reader.GetFieldValue<DateTime>(6)
        });
    }

    [HttpPatch("{id:guid}/ativacao")]
    public async Task<IActionResult> AlterarAtivacao(
        Guid id,
        [FromQuery] bool ativa)
    {
        const string sql = """
            UPDATE public.propagandas
            SET ativa = $1, updated_at = NOW()
            WHERE id = $2;
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(ativa);
        cmd.Parameters.AddWithValue(id);

        var linhas = await cmd.ExecuteNonQueryAsync();

        if (linhas == 0)
            return NotFound(new { erro = "Propaganda não encontrada." });

        return Ok(new { id, ativa });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id)
    {
        const string sql = """
            DELETE FROM public.propagandas WHERE id = $1;
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        var linhas = await cmd.ExecuteNonQueryAsync();

        if (linhas == 0)
            return NotFound(new { erro = "Propaganda não encontrada." });

        return Ok(new { mensagem = "Propaganda excluída com sucesso." });
    }
}
