using System.Text;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Essentials.Services.WolframAlpha;

/// <summary>
/// Converts Wolfram|Alpha pods into MCP <see cref="ContentBlock"/> items.
/// MathML is passed through verbatim — ChatPanel WebView2 renders MathML natively,
/// so no client-side conversion is needed. Visual pods are fetched as images
/// and embedded so expiring session-scoped URLs cannot cause dead references.
/// </summary>
public sealed class ResultConverter
{
    /// <summary>
    /// Pod IDs whose primary content is visual. Images for these pods are
    /// always fetched even when plaintext is present.
    /// </summary>
    static readonly HashSet<string> s_visualPods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Plot", "3DPlot", "ContourPlot", "NumberLine",
        "Image", "VisualRepresentation", "PeriodicTableLocation",
        "UnitCircle", "VectorPlot", "NyquistPlot", "BodePlot",
        "Illustration",
    };

    readonly HttpClient _http;

    public ResultConverter(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Converts a <see cref="QueryResult"/> into ordered MCP content blocks.
    /// Callers should still set <c>IsError</c> on the enclosing <c>CallToolResult</c>
    /// when <see cref="QueryResult.Success"/> is <see langword="false"/>.
    /// </summary>
    public async Task<List<ContentBlock>> ConvertAsync(QueryResult result, CancellationToken ct)
    {
        var blocks = new List<ContentBlock>();

        if (!result.Success)
        {
            blocks.Add(new TextContentBlock { Text = BuildErrorInfo(result) });
            return blocks;
        }

        foreach (var pod in result.Pods ?? [])
        {
            var sb = new StringBuilder();
            sb.Append("### ").AppendLine(pod.Title);

            foreach (var sub in pod.SubPods)
            {
                if (!string.IsNullOrEmpty(sub.Plaintext))
                    sb.AppendLine(sub.Plaintext);

                if (!string.IsNullOrEmpty(sub.MathML))
                    sb.AppendLine(sub.MathML);
            }

            blocks.Add(new TextContentBlock { Text = sb.ToString().TrimEnd() });

            if (ShouldFetchImages(pod))
            {
                foreach (var sub in pod.SubPods)
                {
                    if (sub.Image is { Src.Length: > 0 } img)
                    {
                        var block = await FetchImageAsync(img, ct);
                        if (block is not null)
                            blocks.Add(block);
                    }
                }
            }
        }

        if (result.Assumptions is { Count: > 0 })
            blocks.Add(new TextContentBlock { Text = BuildAssumptions(result.Assumptions) });

        var srcUrl = $"https://www.wolframalpha.com/input?i={Uri.EscapeDataString(result.InputString)}";
        blocks.Add(new TextContentBlock { Text = $"[View on Wolfram|Alpha]({srcUrl})" });

        return blocks;
    }

    /// <summary>
    /// Visual pods always fetch images; non-visual pods fetch only when the
    /// subpods carry no plaintext — in that case the image is the content.
    /// </summary>
    static bool ShouldFetchImages(Pod pod)
    {
        if (s_visualPods.Contains(pod.Id)) return true;
        return pod.SubPods.All(s => string.IsNullOrEmpty(s.Plaintext));
    }

    async Task<ImageContentBlock?> FetchImageAsync(WolframImage img, CancellationToken ct)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(img.Src, ct);
            var mime = string.IsNullOrWhiteSpace(img.ContentType) ? "image/gif" : img.ContentType;
            return ImageContentBlock.FromBytes(bytes, mime);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    static string BuildAssumptions(List<Assumption> assumptions)
    {
        var sb = new StringBuilder("### Disambiguation Required\n");
        foreach (var a in assumptions)
        {
            sb.Append("**").Append(a.Word).Append("** (").Append(a.Type).AppendLine("):");
            foreach (var v in a.Values)
                sb.Append("- ").Append(v.Desc).Append(": `").Append(v.Input).AppendLine("`");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds a descriptive error block for <c>success=false</c> responses.
    /// <c>reinterpret=true</c> is on by default so reaching this path means
    /// the API also failed to auto-correct. Priority: assumptions &gt; tips &gt; didyoumeans
    /// (official docs discourage heavy reliance on didyoumeans).
    /// </summary>
    static string BuildErrorInfo(QueryResult result)
    {
        var sb = new StringBuilder("Wolfram|Alpha could not interpret the query");

        if (result.Assumptions is { Count: > 0 })
        {
            sb.AppendLine(" — disambiguation may help:");
            foreach (var a in result.Assumptions)
            {
                sb.Append("  ").Append(a.Word).Append(" (").Append(a.Type).AppendLine("):");
                foreach (var v in a.Values)
                    sb.Append("  - ").Append(v.Desc).Append(": `").Append(v.Input).AppendLine("`");
            }
        }
        else
        {
            sb.AppendLine(".");
        }

        if (result.Tips is { Count: > 0 })
            foreach (var t in result.Tips)
                sb.Append("Tip: ").AppendLine(t.Text);

        if (result.DidYouMeans is { Count: > 0 })
        {
            sb.AppendLine("Possible alternatives:");
            foreach (var d in result.DidYouMeans)
                sb.Append("- ").AppendLine(d.Val);
        }

        sb.AppendLine();
        sb.AppendLine("Try rephrasing the query in simplified English keyword form.");

        return sb.ToString().TrimEnd();
    }
}
