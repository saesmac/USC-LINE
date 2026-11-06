using DotNetEnv;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

if (File.Exists(Path.Combine(AppContext.BaseDirectory, ".env")) || File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ".env")))
{
    Env.Load();
}

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Supabase");

if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = builder.Configuration["Supabase:Host"] ?? Environment.GetEnvironmentVariable("SUPABASE_HOST");
    var port = builder.Configuration["Supabase:Port"] ?? Environment.GetEnvironmentVariable("SUPABASE_PORT") ?? "5432";
    var database = builder.Configuration["Supabase:Database"] ?? Environment.GetEnvironmentVariable("SUPABASE_DATABASE");
    var username = builder.Configuration["Supabase:Username"] ?? Environment.GetEnvironmentVariable("SUPABASE_USERNAME");
    var password = builder.Configuration["Supabase:Password"] ?? Environment.GetEnvironmentVariable("SUPABASE_PASSWORD");

    if (!string.IsNullOrWhiteSpace(host)
        && !string.IsNullOrWhiteSpace(database)
        && !string.IsNullOrWhiteSpace(username)
        && !string.IsNullOrWhiteSpace(password))
    {
        connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A conexão com o Supabase não foi configurada. Defina ConnectionStrings__Supabase ou as variáveis SUPABASE_HOST, SUPABASE_PORT, SUPABASE_DATABASE, SUPABASE_USERNAME e SUPABASE_PASSWORD."
    );
}

// Registra a conexão com o PostgreSQL/Supabase
builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
{
    return NpgsqlDataSource.Create(connectionString);
});


// Permite acesso pelo site dos alunos publicado na Vercel
builder.Services.AddCors(options =>
{
    options.AddPolicy("SiteAlunos", policy =>
    {
        policy.WithOrigins(
                "https://usc-line.vercel.app",
                "https://unisagrado-alunos.vercel.app",
                "https://unisagrado-alunos-git-front-koi-analytics.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.UseCors("SiteAlunos");

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