using System.ComponentModel;
using System.Net;
using FieldCure.Mcp.Essentials.Services;
using FieldCure.Mcp.Essentials.Services.WolframAlpha;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that queries the Wolfram|Alpha Full Results API. Returns mixed
/// text/image content — MathML is passed through verbatim for native ChatPanel
/// rendering, plots are embedded as images.
/// </summary>
[McpServerToolType]
public static class WolframAlphaTool
{
    const string EnvVarName = "WOLFRAM_APPID";
    const string ProviderLabel = "Wolfram|Alpha";

    const string MissingAppIdMessage =
        "Wolfram|Alpha requires an AppID to function.\n" +
        "1. Go to https://developer.wolframalpha.com and sign in\n" +
        "2. Click 'Get an AppID' → select 'Full Results API' → Submit\n" +
        "3. Set the WOLFRAM_APPID environment variable\n" +
        "4. Restart the MCP server\n\n" +
        "Free tier: 2,000 API calls/month (non-commercial).\n" +
        "⚠️ Use developer.wolframalpha.com, NOT developer.wolfram.com";

    const string InvalidAppIdMessage =
        "Wolfram|Alpha rejected the AppID (401/403). It is invalid, expired, or not authorized for the Full Results API.\n" +
        "Verify at https://developer.wolframalpha.com/portal/myapps/ and ensure it was created for 'Full Results API'.";

    /// <summary>
    /// Executes a Wolfram|Alpha query and returns MCP content blocks. On a
    /// 401/403 response, invalidates the cached AppID once and re-resolves
    /// (which may re-elicit the user on clients supporting Elicitation)
    /// before giving up. Re-elicit caps inside
    /// <see cref="ApiKeyResolverRegistry"/> prevent unbounded loops.
    /// </summary>
    [McpServerTool(Name = "wolfram_alpha")]
    [Description(
        "Query Wolfram|Alpha computational knowledge engine. Returns structured results with MathML formulas (rendered directly in chat) and chart/plot images.\n\n" +
        "RECOMMENDED for: symbolic math, integrals, differential equations, equation solving, matrix operations, eigenvalues, Nyquist/Bode plots, impedance calculations, scientific constants, unit conversions, plotting, nutrition data, financial data, statistics, date/time calculations.\n" +
        "AVOID for: simple arithmetic (use run_javascript), general web search (use web_search), subjective questions, current news.\n\n" +
        "Query guidelines:\n" +
        "- Queries MUST be in English. Translate before sending, respond in user's language.\n" +
        "- Use simplified keyword form: 'France population' not 'how many people live in France'.\n" +
        "- Exponent notation: 6*10^14, NEVER 6e14.\n" +
        "- Single-letter variable names: x, y, n (not 'variable1').\n" +
        "- Named physical constants: 'speed of light', not 299792458.\n" +
        "- Space between compound units: 'Ω m' not 'Ωm'.\n" +
        "- For equations with units, solve without units first.\n" +
        "- If response contains assumptions, re-query with the relevant assumption value.\n" +
        "- The API auto-reinterprets failed queries. If reinterpretation still fails, try rephrasing.\n" +
        "- Include MathML from results directly in your response — it renders natively in chat.")]
    public static async Task<CallToolResult> Query(
        McpServer? server,
        ApiKeyResolverRegistry apiKeys,
        WolframAlphaClient client,
        ResultConverter converter,
        [Description("Query in English. Use simplified keyword form.")]
        string input,
        [Description("Disambiguation assumption from a previous result. Pass the 'input' value as-is (e.g., '*C.pi-_*MathematicalConstant-').")]
        string? assumption = null,
        [Description("Comma-separated pod IDs to include (e.g., 'Result,Plot'). Omit for all pods.")]
        string? includepodid = null,
        [Description("Unit system: 'metric' (default) or 'nonmetric'.")]
        string? units = null,
        [Description("ISO currency code (e.g., 'KRW', 'USD').")]
        string? currency = null,
        [Description("ISO country code (e.g., 'KR', 'US').")]
        string? countrycode = null,
        [Description("Location context (e.g., 'Seoul, Korea').")]
        string? location = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ErrorResult("Query input must not be empty.");

        var gate = server is null ? null : new McpServerElicitGate(server);

        var appId = await apiKeys.ResolveAsync(gate, EnvVarName, ProviderLabel, cancellationToken);
        if (string.IsNullOrWhiteSpace(appId))
            return ErrorResult(MissingAppIdMessage);

        try
        {
            var queryResult = await client.QueryAsync(
                appId, input, assumption, includepodid, units, currency, countrycode, location, cancellationToken);

            var blocks = await converter.ConvertAsync(queryResult, cancellationToken);
            return new CallToolResult { Content = blocks, IsError = !queryResult.Success };
        }
        catch (HttpRequestException ex) when (IsAuthFailure(ex.StatusCode))
        {
            // Invalid/expired AppID. Invalidate the cache and retry once —
            // this gives Elicitation-capable clients a second chance in case
            // the user mistyped the key. ApiKeyResolverRegistry enforces its
            // own re-elicit cap so the retry cannot loop indefinitely.
            apiKeys.Invalidate(EnvVarName);

            var retryAppId = await apiKeys.ResolveAsync(gate, EnvVarName, ProviderLabel, cancellationToken);
            if (string.IsNullOrWhiteSpace(retryAppId))
                return ErrorResult(InvalidAppIdMessage);

            try
            {
                var queryResult = await client.QueryAsync(
                    retryAppId, input, assumption, includepodid, units, currency, countrycode, location, cancellationToken);

                var blocks = await converter.ConvertAsync(queryResult, cancellationToken);
                return new CallToolResult { Content = blocks, IsError = !queryResult.Success };
            }
            catch (HttpRequestException ex2) when (ex2.StatusCode == HttpStatusCode.Forbidden)
            {
                apiKeys.Invalidate(EnvVarName);
                return ErrorResult(InvalidAppIdMessage);
            }
        }
        catch (HttpRequestException ex)
        {
            return ErrorResult($"Wolfram|Alpha request failed: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return ErrorResult("Wolfram|Alpha request timed out.");
        }
    }

    static CallToolResult ErrorResult(string message) => new()
    {
        Content = [new TextContentBlock { Text = message }],
        IsError = true,
    };

    /// <summary>
    /// Returns <see langword="true"/> for HTTP status codes that indicate an
    /// AppID rejection. Wolfram|Alpha returns 401 for a missing or malformed
    /// AppID and 403 for valid-looking but unauthorized AppIDs; both should
    /// trigger the invalidate-and-re-elicit flow.
    /// </summary>
    static bool IsAuthFailure(HttpStatusCode? code) =>
        code == HttpStatusCode.Unauthorized || code == HttpStatusCode.Forbidden;
}
