namespace Secretaria.Api.DTOs;

public class SolicitarSenhaDto
{
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Preferencial { get; set; }
}
