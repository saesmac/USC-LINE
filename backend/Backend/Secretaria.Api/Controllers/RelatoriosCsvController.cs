using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/Relatorios")]
public class RelatoriosCsvController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public RelatoriosCsvController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet("csv")]
    public async Task<IActionResult> ExportarCsv(
        [FromQuery] DateOnly? dataInicio,
        [FromQuery] DateOnly? dataFim,
        CancellationToken cancellationToken)
    {
        var inicio = dataInicio ?? DateOnly.FromDateTime(DateTime.Today);
        var fim = dataFim ?? DateOnly.FromDateTime(DateTime.Today);

        if (inicio > fim)
        {
            return BadRequest(new
            {
                mensagem = "A data inicial não pode ser posterior à data final."
            });
        }

        if (fim.DayNumber - inicio.DayNumber > 366)
        {
            return BadRequest(new
            {
                mensagem = "O período máximo permitido para exportação é de 367 dias."
            });
        }

        const string sql = """
            SELECT
                t.codigo,
                t.nome,
                t.descricao,
                t.preferencial,
                t.status,
                t.created_at,
                t.called_at,
                t.started_at,
                t.finished_at,
                m.numero AS mesa_numero,
                a.resultado,
                a.observacao
            FROM public.tickets t
            LEFT JOIN public.mesas m
                ON m.id = t.mesa_id
            LEFT JOIN LATERAL (
                SELECT at.resultado, at.observacao
                FROM public.atendimentos at
                WHERE at.ticket_id = t.id
                ORDER BY at.created_at DESC
                LIMIT 1
            ) a ON TRUE
            WHERE t.data_fila >= @inicio
              AND t.data_fila <= @fim
            ORDER BY t.data_fila, t.preferencial DESC, t.numero;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.Add("@inicio", NpgsqlDbType.Date).Value = inicio;
        command.Parameters.Add("@fim", NpgsqlDbType.Date).Value = fim;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var csv = new StringBuilder();

        csv.AppendLine(
            "Senha;Nome;Descrição;Preferencial;Status;Criada em;" +
            "Chamada em;Início do atendimento;Fim do atendimento;" +
            "Mesa;Resultado;Observação");

        while (await reader.ReadAsync(cancellationToken))
        {
            var campos = new string[]
            {
                Texto(reader, "codigo"),
                Texto(reader, "nome"),
                Texto(reader, "descricao"),
                reader.GetBoolean(reader.GetOrdinal("preferencial"))
                    ? "Sim" : "Não",
                Texto(reader, "status"),
                DataHora(reader, "created_at"),
                DataHora(reader, "called_at"),
                DataHora(reader, "started_at"),
                DataHora(reader, "finished_at"),
                Texto(reader, "mesa_numero"),
                Texto(reader, "resultado"),
                Texto(reader, "observacao")
            };

            csv.AppendLine(string.Join(";", campos.Select(Escapar)));
        }

        // BOM para facilitar a leitura de acentos no Excel.
        var conteudo = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        var nomeArquivo =
            $"relatorio_senhas_{inicio:yyyy-MM-dd}_{fim:yyyy-MM-dd}.csv";

        return File(conteudo, "text/csv; charset=utf-8", nomeArquivo);
    }

    private static string Texto(NpgsqlDataReader reader, string coluna)
    {
        var indice = reader.GetOrdinal(coluna);

        if (reader.IsDBNull(indice))
            return "";

        return Convert.ToString(
            reader.GetValue(indice),
            CultureInfo.InvariantCulture
        ) ?? "";
    }

    private static string DataHora(NpgsqlDataReader reader, string coluna)
    {
        var indice = reader.GetOrdinal(coluna);

        if (reader.IsDBNull(indice))
            return "";

        var valor = reader.GetValue(indice);

        if (valor is DateTime data)
            return data.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        if (valor is DateTimeOffset dataOffset)
            return dataOffset.ToString("dd/MM/yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture);

        return Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
    }

    private static string Escapar(string valor)
    {
        // Evita que conteúdo digitado pelo usuário seja interpretado
        // como fórmula pelo Excel.
        if (valor.Length > 0 && "=+-@".Contains(valor[0]))
            valor = "'" + valor;

        return "\"" + valor.Replace("\"", "\"\"") + "\"";
    }
}
