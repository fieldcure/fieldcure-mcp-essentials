using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Services;

/// <summary>
/// Production adapter that exposes the elicitation-related surface of an
/// <see cref="McpServer"/> through <see cref="IElicitGate"/>.
/// </summary>
/// <param name="server">The active MCP server instance.</param>
internal sealed class McpServerElicitGate(McpServer server) : IElicitGate
{
    /// <inheritdoc />
    public bool IsSupported => server.ClientCapabilities?.Elicitation is not null;

    /// <inheritdoc />
    public async Task<ElicitGateResult> ElicitAsync(ElicitRequestParams request, CancellationToken ct)
    {
        var result = await server.ElicitAsync(request, ct);
        return new ElicitGateResult(result.IsAccepted, result.Content);
    }
}
