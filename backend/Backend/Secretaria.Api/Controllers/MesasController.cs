using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MesasController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public MesasController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                m.id,
                m.numero,
                m.nome,
                m.ativa,
                t.codigo AS senha_codigo,
                t.nome AS senha_nome,
                t.status AS senha_status,
                t.preferencial
            FROM public.mesas m
            LEFT JOIN LATERAL (
                SELECT
                    tk.codigo,
                    tk.nome,
                    tk.status,
                    tk.preferencial
                FROM public.tickets tk
                WHERE tk.mesa_id = m.id
                  AND tk.data_fila = CURRENT_DATE
                  AND tk.status IN ('CHAMADO', 'EM_ATENDIMENTO')
                ORDER BY tk.called_at DESC NULLS LAST
                LIMIT 1
            ) t ON TRUE
            ORDER BY m.numero;
            """;

        var mesas = new List<object>();

        await using var conexao =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var comando = new NpgsqlCommand(sql, conexao);

        await using var leitor =
            await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            mesas.Add(new
            {
                id = leitor.GetGuid(0),
                numero = leitor.GetInt32(1),
                nome = leitor.GetString(2),
                ativa = leitor.GetBoolean(3),
                senhaAtual = leitor.IsDBNull(4)
                    ? null
                    : new
                    {
                        codigo = leitor.GetString(4),
                        nome = leitor.GetString(5),
                        status = leitor.GetString(6),
                        preferencial = leitor.GetBoolean(7)
                    }
            });
        }

        return Ok(mesas);
    }
}
