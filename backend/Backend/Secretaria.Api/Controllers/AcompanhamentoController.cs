using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AcompanhamentoController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public AcompanhamentoController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet("{codigo}")]
    public async Task<IActionResult> Consultar(
        string codigo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return BadRequest(new
            {
                mensagem = "Informe o código da senha."
            });
        }

        const string sql = """
            SELECT
                t.id,
                t.codigo,
                t.nome,
                t.descricao,
                t.preferencial,
                t.status,
                t.data_fila,
                t.created_at,
                t.called_at,
                t.started_at,
                t.finished_at,
                m.numero AS mesa_numero,
                m.nome AS mesa_nome,
                CASE
                    WHEN t.status = 'AGUARDANDO' THEN (
                        SELECT COUNT(*)::integer
                        FROM public.tickets anterior
                        WHERE anterior.data_fila = t.data_fila
                          AND anterior.status = 'AGUARDANDO'
                          AND (
                              (anterior.preferencial = TRUE
                               AND t.preferencial = FALSE)
                              OR
                              (anterior.preferencial = t.preferencial
                               AND anterior.created_at < t.created_at)
                          )
                    ) + 1
                    ELSE NULL
                END AS posicao_fila
            FROM public.tickets t
            LEFT JOIN public.mesas m ON m.id = t.mesa_id
            WHERE t.codigo = @codigo
              AND t.data_fila = CURRENT_DATE
            LIMIT 1;
            """;

        await using var conexao =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddWithValue("codigo", codigo.Trim().ToUpperInvariant());

        await using var leitor =
            await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken))
        {
            return NotFound(new
            {
                mensagem = "Senha não encontrada para hoje."
            });
        }

        var resultado = new
        {
            id = leitor.GetGuid(0),
            codigo = leitor.GetString(1),
            nome = leitor.GetString(2),
            descricao = leitor.GetString(3),
            preferencial = leitor.GetBoolean(4),
            status = leitor.GetString(5),
            dataFila = leitor.GetDateTime(6),
            criadaEm = leitor.GetDateTime(7),
            chamadaEm = leitor.IsDBNull(8) ? (DateTime?)null : leitor.GetDateTime(8),
            inicioAtendimento = leitor.IsDBNull(9) ? (DateTime?)null : leitor.GetDateTime(9),
            finalizadaEm = leitor.IsDBNull(10) ? (DateTime?)null : leitor.GetDateTime(10),
            mesaNumero = leitor.IsDBNull(11) ? (int?)null : leitor.GetInt32(11),
            mesaNome = leitor.IsDBNull(12) ? null : leitor.GetString(12),
            posicaoFila = leitor.IsDBNull(13) ? (int?)null : leitor.GetInt32(13)
        };

        return Ok(resultado);
    }
}
