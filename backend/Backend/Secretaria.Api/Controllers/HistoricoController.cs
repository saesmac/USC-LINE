using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HistoricoController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public HistoricoController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] DateOnly? dataInicio,
        [FromQuery] DateOnly? dataFim,
        [FromQuery] int? mesaNumero,
        CancellationToken cancellationToken)
    {
        if (dataInicio.HasValue && dataFim.HasValue &&
            dataInicio.Value > dataFim.Value)
        {
            return BadRequest(new
            {
                mensagem = "A data inicial não pode ser posterior à data final."
            });
        }

        if (mesaNumero.HasValue && mesaNumero.Value < 1)
        {
            return BadRequest(new
            {
                mensagem = "O número da mesa deve ser positivo."
            });
        }

        const string sql = """
            SELECT
                a.id,
                t.codigo,
                t.nome,
                t.descricao,
                t.preferencial,
                a.inicio,
                a.fim,
                a.resultado,
                a.observacao,
                m.numero,
                m.nome
            FROM public.atendimentos a
            INNER JOIN public.tickets t ON t.id = a.ticket_id
            INNER JOIN public.mesas m ON m.id = a.mesa_id
            WHERE (@data_inicio::date IS NULL OR a.inicio::date >= @data_inicio::date)
              AND (@data_fim::date IS NULL OR a.inicio::date <= @data_fim::date)
              AND (@mesa_numero::integer IS NULL OR m.numero = @mesa_numero::integer)
            ORDER BY a.inicio DESC
            LIMIT 500;
            """;

        var itens = new List<object>();

        await using var conexao =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var comando = new NpgsqlCommand(sql, conexao);

        comando.Parameters.Add("data_inicio", NpgsqlDbType.Date).Value =
            dataInicio.HasValue
                ? dataInicio.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;

        comando.Parameters.Add("data_fim", NpgsqlDbType.Date).Value =
            dataFim.HasValue
                ? dataFim.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;

        comando.Parameters.Add("mesa_numero", NpgsqlDbType.Integer).Value =
            mesaNumero.HasValue
                ? mesaNumero.Value
                : DBNull.Value;

        await using var leitor =
            await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(new
            {
                id = leitor.GetGuid(0),
                codigo = leitor.GetString(1),
                nome = leitor.GetString(2),
                descricao = leitor.GetString(3),
                preferencial = leitor.GetBoolean(4),
                inicio = leitor.IsDBNull(5)
                    ? (DateTime?)null : leitor.GetDateTime(5),
                fim = leitor.IsDBNull(6)
                    ? (DateTime?)null : leitor.GetDateTime(6),
                resultado = leitor.IsDBNull(7)
                    ? null : leitor.GetString(7),
                observacao = leitor.IsDBNull(8)
                    ? null : leitor.GetString(8),
                mesaNumero = leitor.GetInt32(9),
                mesaNome = leitor.GetString(10)
            });
        }

        return Ok(new
        {
            total = itens.Count,
            limite = 500,
            atendimentos = itens
        });
    }
}
