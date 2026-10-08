namespace Secretaria.Api.DTOs;

public class CancelarSenhaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}
