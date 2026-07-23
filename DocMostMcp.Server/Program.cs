using System.Text.Json;
using DocMostMcp.Server.Client;
using DocMostMcp.Server.Configuration;
using DocMostMcp.Server.Json;
using DocMostMcp.Server.Tools;
using ModelContextProtocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ─────────────────────────────────────────────────────
// 1. Read and validate configuration at startup
// ─────────────────────────────────────────────────────

var options = ReadConfiguration();
ValidateConfiguration(options);

// ─────────────────────────────────────────────────────
// 2. Determine transport mode
// ─────────────────────────────────────────────────────

var transportEnv = Environment.GetEnvironmentVariable("DOCMOST_MCP_TRANSPORT");
options.TransportExplicitlySet = transportEnv is not null;

if (transportEnv is not null)
{
    options.Transport = transportEnv.ToLowerInvariant() switch
    {
        "stdio" => TransportMode.Stdio,
        "http" => TransportMode.Http,
        _ => throw new InvalidOperationException(
            $"DOCMOST_MCP_TRANSPORT must be 'stdio' or 'http'. Current value: '{transportEnv}'.")
    };
}
else
{
    // Auto-detect: TTY present → stdio (local dev), no TTY → http (container/service).
    options.Transport = Console.IsInputRedirected
        ? TransportMode.Http
        : TransportMode.Stdio;
}

// ─────────────────────────────────────────────────────
// 3. Bootstrap the server based on transport
// ─────────────────────────────────────────────────────

if (options.Transport == TransportMode.Stdio)
{
    // ── STDIO MODE ──
    var stdioBuilder = Host.CreateApplicationBuilder(args);

    // All logs go to stderr (stdout is the MCP protocol channel).
    stdioBuilder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    RegisterServices(stdioBuilder.Services, options);

    var stdioSerializerOptions = new JsonSerializerOptions(AppJsonSerializerContext.Default.Options);

    stdioBuilder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<DocmostTools>(stdioSerializerOptions);

    await stdioBuilder.Build().RunAsync();
}
else
{
    // ── HTTP MODE ──
    var httpBuilder = WebApplication.CreateSlimBuilder(args);

    httpBuilder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, McpJsonUtilities.DefaultOptions.TypeInfoResolver!);
    });

    RegisterServices(httpBuilder.Services, options);

    httpBuilder.Services
        .AddMcpServer()
        .WithHttpTransport(o => o.Stateless = true)
        .WithTools<DocmostTools>();

    var app = httpBuilder.Build();
    app.MapMcp("/mcp");
    await app.RunAsync($"http://0.0.0.0:{options.Port}");
}

// ─────────────────────────────────────────────────────
// Local functions
// ─────────────────────────────────────────────────────

static DocmostOptions ReadConfiguration()
{
    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();

    var options = new DocmostOptions();

    // Map flat env vars to nested options for options pattern binding.
    var envOverrides = new Dictionary<string, string?>
    {
        ["Docmost:Url"] = Environment.GetEnvironmentVariable("DOCMOST_URL"),
        ["Docmost:Email"] = Environment.GetEnvironmentVariable("DOCMOST_EMAIL"),
        ["Docmost:Password"] = Environment.GetEnvironmentVariable("DOCMOST_PASSWORD"),
        ["Docmost:Port"] = Environment.GetEnvironmentVariable("DOCMOST_MCP_PORT"),
    };

    var configWithOverrides = new ConfigurationBuilder()
        .AddInMemoryCollection(envOverrides)
        .AddEnvironmentVariables()
        .Build();

    configWithOverrides.Bind("Docmost", options);

    return options;
}

static void ValidateConfiguration(DocmostOptions options)
{
    var validator = new DocmostOptionsValidator();
    var result = validator.Validate(null, options);
    if (result.Failed)
    {
        var message = string.Join(Environment.NewLine, result.Failures);
        throw new InvalidOperationException(
            $"Docmost MCP server configuration is invalid:{Environment.NewLine}{message}");
    }
}

static void RegisterServices(IServiceCollection services, DocmostOptions options)
{
    // Register options for DI (so tools/handlers can inject IOptions<T>).
    services.AddSingleton(Options.Create(options));

    // CookieSessionStore uses its OWN HttpClient that does NOT go through
    // DocmostAuthHandler (to avoid infinite recursion during login).
    services.AddSingleton<CookieSessionStore>(_ =>
    {
        var handler = new SocketsHttpHandler { UseCookies = false };
        var loginClient = new HttpClient(handler)
        {
            BaseAddress = options.Url!,
        };
        return new CookieSessionStore(loginClient);
    });

    // DocmostAuthHandler is a DelegatingHandler, must be transient.
    services.AddTransient<DocmostAuthHandler>();

    // DocmostClient uses the standard HTTP client factory pipeline with
    // DocmostAuthHandler injecting the session cookie.
    // UseCookies = false because CookieSessionStore manages cookies directly.
    services.AddHttpClient<DocmostClient>(client =>
    {
        client.BaseAddress = options.Url!;
    })
    .AddHttpMessageHandler<DocmostAuthHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseCookies = false,
    });
}
