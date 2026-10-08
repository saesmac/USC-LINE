namespace Secretaria.Api.DTOs;

public class FinalizarAtendimentoDto
{
    public int MesaNumero { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? Observacao { get; set; }
}
