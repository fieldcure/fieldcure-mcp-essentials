using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jint;
using Jint.Native;
using Jint.Runtime;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

[McpServerToolType]
public static class RunJavaScriptTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool(Name = "run_javascript")]
    [Description("Execute JavaScript in a sandboxed engine (no file/network access). Use for math calculations, data transformation, JSON processing, regex, encoding/decoding, and date calculations. Variables can be injected into the script scope.")]
    public static string RunJavaScript(
        [Description("JavaScript code (expression or script). The last expression's value is returned.")]
        string? code = null,
        [Description("Timeout in seconds (default: 5, max: 30)")]
        int timeout_seconds = 5,
        [Description("Variables to inject into JS scope as JSON object, e.g. {\"data\": [...], \"x\": 42}")]
        string? variables = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return JsonSerializer.Serialize(new { result = (object?)null, error = "Parameter 'code' is required." }, JsonOptions);

        timeout_seconds = Math.Clamp(timeout_seconds, 1, 30);
        var consoleSb = new StringBuilder();

        try
        {
            var engine = new Engine(options =>
            {
                options.TimeoutInterval(TimeSpan.FromSeconds(timeout_seconds));
                options.MaxStatements(100_000);
                options.LimitRecursion(64);
                options.Strict();
                options.LocalTimeZone(TimeZoneInfo.Local);
            });

            // Inject console.log/warn/error — define in JS scope before user code
            engine.SetValue("__consoleWrite", new Action<string, JsValue>(
                (prefix, argsObj) =>
                {
                    var parts = new List<string>();
                    if (argsObj is Jint.Native.Object.ObjectInstance obj)
                    {
                        uint i = 0;
                        while (obj.HasProperty(i.ToString()))
                        {
                            parts.Add(obj.Get(i.ToString()).ToString());
                            i++;
                        }
                    }
                    consoleSb.AppendLine(prefix + string.Join(" ", parts));
                }));
            engine.Execute(
                "var console = {" +
                "  log: function() { __consoleWrite('', arguments); }," +
                "  warn: function() { __consoleWrite('[warn] ', arguments); }," +
                "  error: function() { __consoleWrite('[error] ', arguments); }" +
                "};");

            // Inject variables
            if (variables is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(variables);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        engine.SetValue(prop.Name, ConvertJsonElement(prop.Value));
                    }
                }
                catch (JsonException)
                {
                    return JsonSerializer.Serialize(new { result = (object?)null, error = "Invalid variables JSON." }, JsonOptions);
                }
            }

            var jsResult = engine.Evaluate(code);

            var resultValue = jsResult.Type switch
            {
                Jint.Runtime.Types.Undefined => null,
                Jint.Runtime.Types.Null => null,
                _ => jsResult.ToObject(),
            };

            var result = new
            {
                Result = resultValue,
                ConsoleOutput = consoleSb.Length > 0 ? consoleSb.ToString().TrimEnd() : null,
                Error = (string?)null,
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (TimeoutException)
        {
            return JsonSerializer.Serialize(new
            {
                Result = (object?)null,
                ConsoleOutput = consoleSb.Length > 0 ? consoleSb.ToString().TrimEnd() : null,
                Error = $"Execution timed out after {timeout_seconds}s.",
            }, JsonOptions);
        }
        catch (StatementsCountOverflowException)
        {
            return JsonSerializer.Serialize(new
            {
                Result = (object?)null,
                ConsoleOutput = consoleSb.Length > 0 ? consoleSb.ToString().TrimEnd() : null,
                Error = "Statement limit exceeded (100,000).",
            }, JsonOptions);
        }
        catch (RecursionDepthOverflowException)
        {
            return JsonSerializer.Serialize(new
            {
                Result = (object?)null,
                ConsoleOutput = consoleSb.Length > 0 ? consoleSb.ToString().TrimEnd() : null,
                Error = "Recursion depth exceeded (64).",
            }, JsonOptions);
        }
        catch (JavaScriptException ex)
        {
            return JsonSerializer.Serialize(new
            {
                Result = (object?)null,
                ConsoleOutput = consoleSb.Length > 0 ? consoleSb.ToString().TrimEnd() : null,
                Error = ex.Message,
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                Result = (object?)null,
                ConsoleOutput = consoleSb.Length > 0 ? consoleSb.ToString().TrimEnd() : null,
                Error = ex.Message,
            }, JsonOptions);
        }
    }

    static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
        _ => element.GetRawText(),
    };
}
