namespace Secretaria.Api.DTOs;

public class CriarPropagandaDto
{
    public string Titulo { get; set; } = string.Empty;
    public string ImagemUrl { get; set; } = string.Empty;
    public bool Ativa { get; set; } = true;
    public int Ordem { get; set; }
}
