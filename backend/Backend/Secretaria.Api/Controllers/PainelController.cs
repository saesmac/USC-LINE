using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PainelController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public PainelController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public async Task<IActionResult> GetPainel(
        CancellationToken cancellationToken)
    {
        await using var conexao =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sqlSenhaAtual = """
            SELECT t.id, t.codigo, t.nome, t.preferencial, t.status,
                   t.called_at, m.numero, m.nome
            FROM public.tickets t
            LEFT JOIN public.mesas m ON m.id = t.mesa_id
            WHERE t.data_fila = CURRENT_DATE
              AND t.status IN ('CHAMADO', 'EM_ATENDIMENTO')
            ORDER BY t.called_at DESC NULLS LAST
            LIMIT 1;
            """;

        object? senhaAtual = null;

        await using (var cmd = new NpgsqlCommand(sqlSenhaAtual, conexao))
        await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await r.ReadAsync(cancellationToken))
            {
                senhaAtual = new
                {
                    id = r.GetGuid(0),
                    codigo = r.GetString(1),
                    nome = r.GetString(2),
                    preferencial = r.GetBoolean(3),
                    status = r.GetString(4),
                    chamadoEm = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5),
                    mesaNumero = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
                    mesaNome = r.IsDBNull(7) ? null : r.GetString(7)
                };
            }
        }

        const string sqlChamadas = """
            SELECT t.codigo, t.nome, t.preferencial, t.status,
                   t.called_at, m.numero, m.nome
            FROM public.tickets t
            LEFT JOIN public.mesas m ON m.id = t.mesa_id
            WHERE t.data_fila = CURRENT_DATE
              AND t.called_at IS NOT NULL
            ORDER BY t.called_at DESC
            LIMIT 10;
            """;

        var ultimasChamadas = new List<object>();

        await using (var cmd = new NpgsqlCommand(sqlChamadas, conexao))
        await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await r.ReadAsync(cancellationToken))
            {
                ultimasChamadas.Add(new
                {
                    codigo = r.GetString(0),
                    nome = r.GetString(1),
                    preferencial = r.GetBoolean(2),
                    status = r.GetString(3),
                    chamadoEm = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4),
                    mesaNumero = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                    mesaNome = r.IsDBNull(6) ? null : r.GetString(6)
                });
            }
        }

        const string sqlPropagandas = """
            SELECT id, titulo, imagem_url, ordem
            FROM public.propagandas
            WHERE ativa = TRUE
            ORDER BY ordem ASC, created_at DESC;
            """;

        var propagandas = new List<object>();

        await using (var cmd = new NpgsqlCommand(sqlPropagandas, conexao))
        await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await r.ReadAsync(cancellationToken))
            {
                propagandas.Add(new
                {
                    id = r.GetGuid(0),
                    titulo = r.GetString(1),
                    imagemUrl = r.GetString(2),
                    ordem = r.GetInt32(3)
                });
            }
        }

        return Ok(new
        {
            data = DateTimeOffset.Now,
            senhaAtual,
            ultimasChamadas,
            propagandas
        });
    }
}
