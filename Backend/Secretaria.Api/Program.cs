using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Lê a conexão do appsettings.Development.json
var connectionString = builder.Configuration.GetConnectionString("Supabase");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A conexão com o Supabase não foi configurada."
    );
}

// Registra a conexão com o PostgreSQL/Supabase
builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
{
    return NpgsqlDataSource.Create(connectionString);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        sistema = "Secretaria UNISAGRADO",
        mensagem = "API funcionando"
    });
});

app.Run();