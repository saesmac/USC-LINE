using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RelatoriosController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public RelatoriosController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public async Task<IActionResult> Consultar(
        [FromQuery] DateOnly? dataInicio,
        [FromQuery] DateOnly? dataFim,
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

        const string sqlResumo = """
            SELECT
                COUNT(*)::integer AS total_senhas,
                COUNT(*) FILTER (WHERE status = 'FINALIZADO')::integer AS finalizadas,
                COUNT(*) FILTER (WHERE status = 'CANCELADO')::integer AS canceladas,
                COUNT(*) FILTER (WHERE status = 'AGUARDANDO')::integer AS aguardando,
                COUNT(*) FILTER (WHERE status = 'CHAMADO')::integer AS chamadas,
                COUNT(*) FILTER (WHERE status = 'EM_ATENDIMENTO')::integer AS em_atendimento,
                ROUND(
                    (AVG(EXTRACT(EPOCH FROM (called_at - created_at)) / 60)
                     FILTER (WHERE called_at IS NOT NULL))::numeric, 2
                ) AS espera_media_minutos,
                ROUND(
                    (AVG(EXTRACT(EPOCH FROM (finished_at - started_at)) / 60)
                     FILTER (WHERE started_at IS NOT NULL AND finished_at IS NOT NULL))::numeric, 2
                ) AS atendimento_medio_minutos
            FROM public.tickets
            WHERE (@data_inicio::date IS NULL OR data_fila >= @data_inicio::date)
              AND (@data_fim::date IS NULL OR data_fila <= @data_fim::date);
            """;

        const string sqlMesas = """
            SELECT
                m.numero,
                m.nome,
                COUNT(t.id)::integer AS total_atendimentos,
                COUNT(t.id) FILTER (WHERE t.status = 'FINALIZADO')::integer AS finalizados,
                COUNT(t.id) FILTER (WHERE t.status = 'CANCELADO')::integer AS cancelados
            FROM public.mesas m
            LEFT JOIN public.tickets t
                ON t.mesa_id = m.id
               AND (@data_inicio::date IS NULL OR t.data_fila >= @data_inicio::date)
               AND (@data_fim::date IS NULL OR t.data_fila <= @data_fim::date)
            GROUP BY m.id, m.numero, m.nome
            ORDER BY m.numero;
            """;

        await using var conexao =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var comandoResumo =
            new NpgsqlCommand(sqlResumo, conexao);

        comandoResumo.Parameters.Add("data_inicio", NpgsqlDbType.Date).Value =
            dataInicio.HasValue
                ? dataInicio.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;

        comandoResumo.Parameters.Add("data_fim", NpgsqlDbType.Date).Value =
            dataFim.HasValue
                ? dataFim.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;

        object resumo;

        await using (var leitor = await comandoResumo.ExecuteReaderAsync(cancellationToken))
        {
            await leitor.ReadAsync(cancellationToken);

            resumo = new
            {
                totalSenhas = leitor.GetInt32(0),
                finalizadas = leitor.GetInt32(1),
                canceladas = leitor.GetInt32(2),
                aguardando = leitor.GetInt32(3),
                chamadas = leitor.GetInt32(4),
                emAtendimento = leitor.GetInt32(5),
                esperaMediaMinutos = leitor.IsDBNull(6) ? (decimal?)null : leitor.GetDecimal(6),
                atendimentoMedioMinutos = leitor.IsDBNull(7) ? (decimal?)null : leitor.GetDecimal(7)
            };
        }

        await using var comandoMesas = new NpgsqlCommand(sqlMesas, conexao);

        comandoMesas.Parameters.Add("data_inicio", NpgsqlDbType.Date).Value =
            dataInicio.HasValue
                ? dataInicio.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;

        comandoMesas.Parameters.Add("data_fim", NpgsqlDbType.Date).Value =
            dataFim.HasValue
                ? dataFim.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;

        var mesas = new List<object>();

        await using (var leitor = await comandoMesas.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                mesas.Add(new
                {
                    numero = leitor.GetInt32(0),
                    nome = leitor.GetString(1),
                    total = leitor.GetInt32(2),
                    finalizados = leitor.GetInt32(3),
                    cancelados = leitor.GetInt32(4)
                });
            }
        }

        return Ok(new
        {
            periodo = new
            {
                dataInicio = dataInicio?.ToString("yyyy-MM-dd"),
                dataFim = dataFim?.ToString("yyyy-MM-dd")
            },
            resumo,
            porMesa = mesas
        });
    }
}
