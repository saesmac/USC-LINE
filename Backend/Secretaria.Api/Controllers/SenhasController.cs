using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Secretaria.Api.DTOs;

namespace Secretaria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SenhasController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public SenhasController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // ============================================================
    // SOLICITAR SENHA
    // POST /api/Senhas
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarSenhaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return BadRequest(new { erro = "Nome é obrigatório." });

        if (string.IsNullOrWhiteSpace(dto.Descricao))
            return BadRequest(new { erro = "Descrição do problema é obrigatória." });

        await using var connection = await _dataSource.OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "SELECT * FROM solicitar_senha($1, $2, $3);",
            connection
        );

        command.Parameters.AddWithValue(dto.Nome.Trim());
        command.Parameters.AddWithValue(dto.Descricao.Trim());
        command.Parameters.AddWithValue(dto.Preferencial);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return StatusCode(500, new { erro = "Não foi possível gerar a senha." });

        return Ok(new
        {
            id = reader["id"],
            codigo = reader["codigo"],
            numero = reader["numero"],
            nome = reader["nome"],
            descricao = reader["descricao"],
            preferencial = reader["preferencial"],
            status = reader["status"],
            dataFila = reader["data_fila"],
            criadoEm = reader["created_at"]
        });
    }

    // ============================================================
    // LISTAR SENHAS AGUARDANDO
    // GET /api/Senhas
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        const string sql = """
            SELECT
                id,
                codigo,
                numero,
                nome,
                descricao,
                preferencial,
                status,
                data_fila,
                created_at
            FROM tickets
            WHERE status = 'AGUARDANDO'
              AND data_fila = CURRENT_DATE
            ORDER BY
                preferencial DESC,
                created_at ASC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var senhas = new List<object>();

        while (await reader.ReadAsync())
        {
            senhas.Add(new
            {
                id = reader["id"],
                codigo = reader["codigo"],
                numero = reader["numero"],
                nome = reader["nome"],
                descricao = reader["descricao"],
                preferencial = reader["preferencial"],
                status = reader["status"],
                dataFila = reader["data_fila"],
                criadoEm = reader["created_at"]
            });
        }

        return Ok(senhas);
    }

    // ============================================================
    // CHAMAR PRÓXIMA SENHA AUTOMATICAMENTE
    // POST /api/Senhas/chamar
    // ============================================================
    [HttpPost("chamar")]
    public async Task<IActionResult> Chamar([FromBody] ChamarSenhaDto dto)
    {
        if (dto.MesaNumero <= 0)
            return BadRequest(new { erro = "Número da mesa inválido." });

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string mesaSql = """
                SELECT
                    id,
                    numero,
                    nome
                FROM mesas
                WHERE numero = $1
                  AND ativa = true
                FOR UPDATE;
                """;

            await using var mesaCommand = new NpgsqlCommand(
                mesaSql,
                connection,
                transaction
            );

            mesaCommand.Parameters.AddWithValue(dto.MesaNumero);

            await using var mesaReader = await mesaCommand.ExecuteReaderAsync();

            if (!await mesaReader.ReadAsync())
            {
                await mesaReader.CloseAsync();
                await transaction.RollbackAsync();

                return NotFound(new
                {
                    erro = "Mesa não encontrada ou está inativa."
                });
            }

            var mesaId = mesaReader.GetGuid(0);
            var mesaNumero = mesaReader.GetInt32(1);
            var mesaNome = mesaReader.GetString(2);

            await mesaReader.CloseAsync();

            const string ocupadaSql = """
                SELECT COUNT(*)
                FROM tickets
                WHERE mesa_id = $1
                  AND status IN ('CHAMADO', 'EM_ATENDIMENTO');
                """;

            await using var ocupadaCommand = new NpgsqlCommand(
                ocupadaSql,
                connection,
                transaction
            );

            ocupadaCommand.Parameters.AddWithValue(mesaId);

            var ocupada = Convert.ToInt32(
                await ocupadaCommand.ExecuteScalarAsync()
            );

            if (ocupada > 0)
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    erro = "Esta mesa já possui uma senha chamada ou em atendimento."
                });
            }

            const string senhaSql = """
                SELECT
                    id,
                    codigo,
                    numero,
                    nome,
                    descricao,
                    preferencial,
                    status,
                    data_fila,
                    created_at
                FROM tickets
                WHERE status = 'AGUARDANDO'
                  AND data_fila = CURRENT_DATE
                ORDER BY
                    preferencial DESC,
                    created_at ASC
                FOR UPDATE SKIP LOCKED
                LIMIT 1;
                """;

            await using var senhaCommand = new NpgsqlCommand(
                senhaSql,
                connection,
                transaction
            );

            await using var senhaReader = await senhaCommand.ExecuteReaderAsync();

            if (!await senhaReader.ReadAsync())
            {
                await senhaReader.CloseAsync();
                await transaction.RollbackAsync();

                return NotFound(new
                {
                    erro = "Não existem senhas aguardando atendimento."
                });
            }

            var ticketId = senhaReader.GetGuid(0);
            var codigo = senhaReader.GetString(1);
            var numero = senhaReader.GetInt32(2);
            var nome = senhaReader.GetString(3);
            var descricao = senhaReader.GetString(4);
            var preferencial = senhaReader.GetBoolean(5);
            var dataFila = senhaReader.GetFieldValue<DateTime>(7);
            var createdAt = senhaReader.GetFieldValue<DateTime>(8);

            await senhaReader.CloseAsync();

            const string updateSql = """
                UPDATE tickets
                SET
                    status = 'CHAMADO',
                    mesa_id = $1,
                    called_at = NOW()
                WHERE id = $2
                RETURNING called_at;
                """;

            await using var updateCommand = new NpgsqlCommand(
                updateSql,
                connection,
                transaction
            );

            updateCommand.Parameters.AddWithValue(mesaId);
            updateCommand.Parameters.AddWithValue(ticketId);

            var calledAt = await updateCommand.ExecuteScalarAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                id = ticketId,
                codigo,
                numero,
                nome,
                descricao,
                preferencial,
                status = "CHAMADO",
                mesa = new
                {
                    id = mesaId,
                    numero = mesaNumero,
                    nome = mesaNome
                },
                dataFila,
                criadoEm = createdAt,
                chamadoEm = calledAt,
                tipoChamada = "AUTOMATICA"
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // CHAMAR SENHA MANUALMENTE
    // POST /api/Senhas/chamar-manual
    // ============================================================
    [HttpPost("chamar-manual")]
    public async Task<IActionResult> ChamarManual(
        [FromBody] ChamarSenhaManualDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
        {
            return BadRequest(new
            {
                erro = "Código da senha é obrigatório."
            });
        }

        if (dto.MesaNumero <= 0)
        {
            return BadRequest(new
            {
                erro = "Número da mesa inválido."
            });
        }

        var codigoInformado = dto.Codigo.Trim().ToUpperInvariant();

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // --------------------------------------------------------
            // 1. Verifica a mesa
            // --------------------------------------------------------
            const string mesaSql = """
                SELECT
                    id,
                    numero,
                    nome
                FROM mesas
                WHERE numero = $1
                  AND ativa = true
                FOR UPDATE;
                """;

            await using var mesaCommand = new NpgsqlCommand(
                mesaSql,
                connection,
                transaction
            );

            mesaCommand.Parameters.AddWithValue(dto.MesaNumero);

            await using var mesaReader = await mesaCommand.ExecuteReaderAsync();

            if (!await mesaReader.ReadAsync())
            {
                await mesaReader.CloseAsync();
                await transaction.RollbackAsync();

                return NotFound(new
                {
                    erro = "Mesa não encontrada ou está inativa."
                });
            }

            var mesaId = mesaReader.GetGuid(0);
            var mesaNumero = mesaReader.GetInt32(1);
            var mesaNome = mesaReader.GetString(2);

            await mesaReader.CloseAsync();

            // --------------------------------------------------------
            // 2. Verifica se a mesa está livre
            // --------------------------------------------------------
            const string ocupadaSql = """
                SELECT COUNT(*)
                FROM tickets
                WHERE mesa_id = $1
                  AND status IN ('CHAMADO', 'EM_ATENDIMENTO');
                """;

            await using var ocupadaCommand = new NpgsqlCommand(
                ocupadaSql,
                connection,
                transaction
            );

            ocupadaCommand.Parameters.AddWithValue(mesaId);

            var ocupada = Convert.ToInt32(
                await ocupadaCommand.ExecuteScalarAsync()
            );

            if (ocupada > 0)
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    erro = "Esta mesa já possui uma senha chamada ou em atendimento."
                });
            }

            // --------------------------------------------------------
            // 3. Busca a senha específica DO DIA ATUAL
            // --------------------------------------------------------
            const string senhaSql = """
                SELECT
                    id,
                    codigo,
                    numero,
                    nome,
                    descricao,
                    preferencial,
                    status,
                    data_fila,
                    created_at
                FROM tickets
                WHERE codigo = $1
                  AND data_fila = CURRENT_DATE
                FOR UPDATE;
                """;

            await using var senhaCommand = new NpgsqlCommand(
                senhaSql,
                connection,
                transaction
            );

            senhaCommand.Parameters.AddWithValue(codigoInformado);

            await using var senhaReader = await senhaCommand.ExecuteReaderAsync();

            if (!await senhaReader.ReadAsync())
            {
                await senhaReader.CloseAsync();
                await transaction.RollbackAsync();

                return NotFound(new
                {
                    erro = $"Senha {codigoInformado} não encontrada para hoje."
                });
            }

            var ticketId = senhaReader.GetGuid(0);
            var codigo = senhaReader.GetString(1);
            var numero = senhaReader.GetInt32(2);
            var nome = senhaReader.GetString(3);
            var descricao = senhaReader.GetString(4);
            var preferencial = senhaReader.GetBoolean(5);
            var status = senhaReader.GetString(6);
            var dataFila = senhaReader.GetFieldValue<DateTime>(7);
            var createdAt = senhaReader.GetFieldValue<DateTime>(8);

            await senhaReader.CloseAsync();

            // --------------------------------------------------------
            // 4. Só pode chamar senha AGUARDANDO
            // --------------------------------------------------------
            if (status != "AGUARDANDO")
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    erro = $"A senha {codigo} não pode ser chamada porque está com status '{status}'."
                });
            }

            // --------------------------------------------------------
            // 5. Atualiza a senha
            // --------------------------------------------------------
            const string updateSql = """
                UPDATE tickets
                SET
                    status = 'CHAMADO',
                    mesa_id = $1,
                    called_at = NOW()
                WHERE id = $2
                RETURNING called_at;
                """;

            await using var updateCommand = new NpgsqlCommand(
                updateSql,
                connection,
                transaction
            );

            updateCommand.Parameters.AddWithValue(mesaId);
            updateCommand.Parameters.AddWithValue(ticketId);

            var calledAt = await updateCommand.ExecuteScalarAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                id = ticketId,
                codigo,
                numero,
                nome,
                descricao,
                preferencial,
                status = "CHAMADO",
                mesa = new
                {
                    id = mesaId,
                    numero = mesaNumero,
                    nome = mesaNome
                },
                dataFila,
                criadoEm = createdAt,
                chamadoEm = calledAt,
                tipoChamada = "MANUAL"
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // INICIAR ATENDIMENTO
    // POST /api/Senhas/iniciar
    // ============================================================
    [HttpPost("iniciar")]
    public async Task<IActionResult> Iniciar(
        [FromBody] IniciarAtendimentoDto dto)
    {
        if (dto.MesaNumero <= 0)
            return BadRequest(new { erro = "Número da mesa inválido." });

        await using var connection = await _dataSource.OpenConnectionAsync();

        const string sql = """
            UPDATE tickets
            SET
                status = 'EM_ATENDIMENTO',
                started_at = NOW()
            WHERE mesa_id = (
                SELECT id
                FROM mesas
                WHERE numero = $1
                  AND ativa = true
            )
              AND status = 'CHAMADO'
            RETURNING
                id,
                codigo,
                numero,
                nome,
                descricao,
                preferencial,
                status,
                mesa_id,
                called_at,
                started_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(dto.MesaNumero);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return NotFound(new
            {
                erro = "Não existe senha chamada para esta mesa."
            });
        }

        return Ok(new
        {
            id = reader["id"],
            codigo = reader["codigo"],
            numero = reader["numero"],
            nome = reader["nome"],
            descricao = reader["descricao"],
            preferencial = reader["preferencial"],
            status = reader["status"],
            mesaId = reader["mesa_id"],
            chamadoEm = reader["called_at"],
            iniciadoEm = reader["started_at"]
        });
    }

    // ============================================================
    // FINALIZAR ATENDIMENTO
    // POST /api/Senhas/finalizar
    // ============================================================
    [HttpPost("finalizar")]
    public async Task<IActionResult> Finalizar(
        [FromBody] FinalizarAtendimentoDto dto)
    {
        if (dto.MesaNumero <= 0)
            return BadRequest(new { erro = "Número da mesa inválido." });

        if (string.IsNullOrWhiteSpace(dto.Resultado))
            return BadRequest(new { erro = "Resultado é obrigatório." });

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string buscaSql = """
                SELECT
                    t.id,
                    t.codigo,
                    t.numero,
                    t.nome,
                    t.descricao,
                    t.preferencial,
                    t.status,
                    t.mesa_id,
                    t.called_at,
                    t.started_at,
                    m.numero,
                    m.nome
                FROM tickets t
                INNER JOIN mesas m
                    ON m.id = t.mesa_id
                WHERE m.numero = $1
                  AND t.status = 'EM_ATENDIMENTO'
                FOR UPDATE;
                """;

            await using var buscaCommand = new NpgsqlCommand(
                buscaSql,
                connection,
                transaction
            );

            buscaCommand.Parameters.AddWithValue(dto.MesaNumero);

            await using var reader = await buscaCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                await reader.CloseAsync();
                await transaction.RollbackAsync();

                return NotFound(new
                {
                    erro = "Não existe atendimento em andamento nesta mesa."
                });
            }

            var ticketId = reader.GetGuid(0);
            var codigo = reader.GetString(1);
            var numero = reader.GetInt32(2);
            var nome = reader.GetString(3);
            var descricao = reader.GetString(4);
            var preferencial = reader.GetBoolean(5);
            var mesaId = reader.GetGuid(7);

            var calledAt = reader.IsDBNull(8)
                ? (DateTime?)null
                : reader.GetFieldValue<DateTime>(8);

            var startedAt = reader.IsDBNull(9)
                ? (DateTime?)null
                : reader.GetFieldValue<DateTime>(9);

            var mesaNumero = reader.GetInt32(10);
            var mesaNome = reader.GetString(11);

            await reader.CloseAsync();

            const string updateSql = """
                UPDATE tickets
                SET
                    status = 'FINALIZADO',
                    finished_at = NOW()
                WHERE id = $1
                RETURNING finished_at;
                """;

            await using var updateCommand = new NpgsqlCommand(
                updateSql,
                connection,
                transaction
            );

            updateCommand.Parameters.AddWithValue(ticketId);

            var finishedAt = await updateCommand.ExecuteScalarAsync();

            const string historicoSql = """
                INSERT INTO atendimentos
                (
                    ticket_id,
                    mesa_id,
                    inicio,
                    fim,
                    resultado,
                    observacao
                )
                VALUES
                (
                    $1,
                    $2,
                    $3,
                    $4,
                    $5,
                    $6
                );
                """;

            await using var historicoCommand = new NpgsqlCommand(
                historicoSql,
                connection,
                transaction
            );

            historicoCommand.Parameters.AddWithValue(ticketId);
            historicoCommand.Parameters.AddWithValue(mesaId);
            historicoCommand.Parameters.AddWithValue(
                startedAt ?? DateTime.UtcNow
            );
            historicoCommand.Parameters.AddWithValue(
                finishedAt ?? DateTime.UtcNow
            );
            historicoCommand.Parameters.AddWithValue(
                dto.Resultado.Trim()
            );
            historicoCommand.Parameters.AddWithValue(
                (object?)dto.Observacao?.Trim() ?? DBNull.Value
            );

            await historicoCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                id = ticketId,
                codigo,
                numero,
                nome,
                descricao,
                preferencial,
                status = "FINALIZADO",
                mesa = new
                {
                    id = mesaId,
                    numero = mesaNumero,
                    nome = mesaNome
                },
                chamadoEm = calledAt,
                iniciadoEm = startedAt,
                finalizadoEm = finishedAt,
                resultado = dto.Resultado.Trim(),
                observacao = dto.Observacao?.Trim()
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // CANCELAR SENHA
    // POST /api/Senhas/cancelar
    // ============================================================
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar(
        [FromBody] CancelarSenhaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
        {
            return BadRequest(new
            {
                erro = "Código da senha é obrigatório."
            });
        }

        var codigoInformado = dto.Codigo.Trim().ToUpperInvariant();

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string buscaSql = """
                SELECT
                    id,
                    codigo,
                    numero,
                    nome,
                    descricao,
                    preferencial,
                    status,
                    mesa_id,
                    data_fila,
                    created_at
                FROM tickets
                WHERE codigo = $1
                  AND data_fila = CURRENT_DATE
                FOR UPDATE;
                """;

            await using var buscaCommand = new NpgsqlCommand(
                buscaSql,
                connection,
                transaction
            );

            buscaCommand.Parameters.AddWithValue(codigoInformado);

            await using var reader = await buscaCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                await reader.CloseAsync();
                await transaction.RollbackAsync();

                return NotFound(new
                {
                    erro = $"Senha {codigoInformado} não encontrada para hoje."
                });
            }

            var ticketId = reader.GetGuid(0);
            var codigo = reader.GetString(1);
            var numero = reader.GetInt32(2);
            var nome = reader.GetString(3);
            var descricao = reader.GetString(4);
            var preferencial = reader.GetBoolean(5);
            var status = reader.GetString(6);

            var mesaId = reader.IsDBNull(7)
                ? (Guid?)null
                : reader.GetGuid(7);

            var dataFila = reader.GetFieldValue<DateTime>(8);
            var createdAt = reader.GetFieldValue<DateTime>(9);

            await reader.CloseAsync();

            if (status == "EM_ATENDIMENTO")
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    erro = "Não é possível cancelar uma senha que já está em atendimento."
                });
            }

            if (status == "FINALIZADO")
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    erro = "Não é possível cancelar uma senha que já foi finalizada."
                });
            }

            if (status == "CANCELADO")
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    erro = "Esta senha já está cancelada."
                });
            }

            if (status != "AGUARDANDO" && status != "CHAMADO")
            {
                await transaction.RollbackAsync();

                return Conflict(new
                {
                    erro = $"A senha está no status '{status}' e não pode ser cancelada."
                });
            }

            const string updateSql = """
                UPDATE tickets
                SET
                    status = 'CANCELADO',
                    cancelled_at = NOW()
                WHERE id = $1
                RETURNING cancelled_at;
                """;

            await using var updateCommand = new NpgsqlCommand(
                updateSql,
                connection,
                transaction
            );

            updateCommand.Parameters.AddWithValue(ticketId);

            var cancelledAt = await updateCommand.ExecuteScalarAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                id = ticketId,
                codigo,
                numero,
                nome,
                descricao,
                preferencial,
                status = "CANCELADO",
                mesaId,
                dataFila,
                criadoEm = createdAt,
                canceladoEm = cancelledAt,
                motivo = dto.Motivo?.Trim()
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
