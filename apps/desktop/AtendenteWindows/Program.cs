using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace UNISAGRADO.Atendente;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new AtendenteForm());
    }
}

internal sealed class AtendenteForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private LocalAppServer? _server;

    public AtendenteForm()
    {
        Text = "UNISAGRADO • Secretaria — Atendente";
        Width = 1280;
        Height = 840;
        MinimumSize = new Size(800, 580);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_web);
        Shown += OnShown;
        FormClosed += (_, _) => _server?.Dispose();
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var staticPath = Path.Combine(baseDir, "wwwroot");
            if (!File.Exists(Path.Combine(staticPath, "atendente-desktop.html")))
                throw new InvalidOperationException("Interface não encontrada. Mantenha a pasta wwwroot junto do executável.");

            var apiUrl = ReadApiUrl(Path.Combine(baseDir, "config.json"));
            _server = new LocalAppServer(staticPath, apiUrl);
            _server.Start();
            await _web.EnsureCoreWebView2Async();
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _web.CoreWebView2.Navigate(_server.LocalUrl + "atendente-desktop.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Não foi possível abrir o aplicativo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string ReadApiUrl(string configPath)
    {
        if (!File.Exists(configPath)) return "";
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!document.RootElement.TryGetProperty("apiUrl", out var property)) return "";
        var value = property.GetString()?.Trim().TrimEnd('/') ?? "";
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("apiUrl inválida no config.json. Use http:// ou https://.");
        return value;
    }
}

internal sealed class LocalAppServer : IDisposable
{
    private readonly string _webRoot;
    private readonly string _apiUrl;
    private readonly HttpListener _listener = new();
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
    private readonly CancellationTokenSource _stop = new();
    public string LocalUrl { get; }

    public LocalAppServer(string webRoot, string apiUrl)
    {
        _webRoot = Path.GetFullPath(webRoot);
        _apiUrl = apiUrl;
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        LocalUrl = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(LocalUrl);
    }

    public void Start()
    {
        _listener.Start();
        _ = LoopAsync();
    }

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                await ProxyApiAsync(context);
                return;
            }

            if (context.Request.HttpMethod != "GET" && context.Request.HttpMethod != "HEAD")
            {
                context.Response.StatusCode = 405;
                return;
            }
            var relative = Uri.UnescapeDataString(path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(relative)) relative = "atendente-desktop.html";
            var target = Path.GetFullPath(Path.Combine(_webRoot, relative));
            if (!target.StartsWith(_webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(target))
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = Path.GetExtension(target).ToLowerInvariant() switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" => "text/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".ico" => "image/x-icon",
                ".woff2" => "font/woff2",
                _ => "application/octet-stream"
            };
            if (context.Request.HttpMethod == "GET")
                await using (var stream = File.OpenRead(target)) await stream.CopyToAsync(context.Response.OutputStream);
        }
        catch (Exception ex)
        {
            try
            {
                if (!context.Response.OutputStream.CanWrite) return;
                context.Response.StatusCode = 500;
                var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                await context.Response.OutputStream.WriteAsync(bytes);
            }
            catch { /* response may already be closed */ }
        }
        finally { try { context.Response.Close(); } catch { } }
    }

    private async Task ProxyApiAsync(HttpListenerContext context)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        if (string.IsNullOrWhiteSpace(_apiUrl))
        {
            context.Response.StatusCode = 503;
            await WriteJsonError(context, "Configure apiUrl no arquivo config.json.");
            return;
        }
        try
        {
            // A API pode estar na rede ou hospedada; nunca colocar credenciais do Supabase no aplicativo.
            var url = _apiUrl + context.Request.RawUrl;
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), url);
            if (context.Request.HasEntityBody)
            {
                request.Content = new StreamContent(context.Request.InputStream);
                if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _stop.Token);
            context.Response.StatusCode = (int)response.StatusCode;
            if (response.Content.Headers.ContentType is not null)
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            if (response.Content.Headers.ContentDisposition is not null)
                context.Response.AddHeader("Content-Disposition", response.Content.Headers.ContentDisposition.ToString());
            await response.Content.CopyToAsync(context.Response.OutputStream);
        }
        catch (Exception)
        {
            context.Response.StatusCode = 502;
            await WriteJsonError(context, "Não foi possível conectar à API. Confira o endereço no config.json e se a API está funcionando.");
        }
    }

    private static async Task WriteJsonError(HttpListenerContext context, string message)
    {
        var body = JsonSerializer.Serialize(new { erro = message });
        await using var writer = new StreamWriter(context.Response.OutputStream, System.Text.Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync(body);
        await writer.FlushAsync();
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
        _http.Dispose();
        _stop.Dispose();
    }
}
