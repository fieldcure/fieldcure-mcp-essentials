using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Default CWD to user home when launched by a host app (e.g., AssistStudio)
// to avoid running in System32 or other system directories.
var cwd = Environment.GetEnvironmentVariable("ESSENTIALS_CWD")
    ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
if (Directory.Exists(cwd))
    Environment.CurrentDirectory = cwd;

var builder = Host.CreateApplicationBuilder(Array.Empty<string>());

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "fieldcure-mcp-essentials",
            Version = typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0",
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
return 0;
