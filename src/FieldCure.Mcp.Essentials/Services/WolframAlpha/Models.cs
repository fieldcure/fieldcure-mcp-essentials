using System.Text.Json.Serialization;

namespace FieldCure.Mcp.Essentials.Services.WolframAlpha;

/// <summary>
/// Top-level envelope returned by the Wolfram|Alpha Full Results API.
/// </summary>
public sealed class WolframResponse
{
    [JsonPropertyName("queryresult")]
    public QueryResult QueryResult { get; set; } = null!;
}

/// <summary>
/// Root query result from the Wolfram|Alpha Full Results API.
/// </summary>
public sealed class QueryResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public bool Error { get; set; }

    [JsonPropertyName("numpods")]
    public int NumPods { get; set; }

    [JsonPropertyName("pods")]
    public List<Pod>? Pods { get; set; }

    [JsonPropertyName("assumptions")]
    public List<Assumption>? Assumptions { get; set; }

    [JsonPropertyName("didyoumeans")]
    public List<DidYouMean>? DidYouMeans { get; set; }

    [JsonPropertyName("tips")]
    public List<Tip>? Tips { get; set; }

    [JsonPropertyName("inputstring")]
    public string InputString { get; set; } = "";

    [JsonPropertyName("timing")]
    public double Timing { get; set; }

    [JsonPropertyName("parsetiming")]
    public double ParseTiming { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

/// <summary>
/// A single pod (section) of Wolfram|Alpha results.
/// </summary>
public sealed class Pod
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("primary")]
    public bool? Primary { get; set; }

    [JsonPropertyName("scanner")]
    public string? Scanner { get; set; }

    [JsonPropertyName("error")]
    public bool Error { get; set; }

    [JsonPropertyName("subpods")]
    public List<SubPod> SubPods { get; set; } = [];

    [JsonPropertyName("states")]
    public List<PodState>? States { get; set; }
}

/// <summary>
/// Subpod holding the actual content in multiple formats.
/// </summary>
public sealed class SubPod
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("plaintext")]
    public string? Plaintext { get; set; }

    [JsonPropertyName("mathml")]
    public string? MathML { get; set; }

    [JsonPropertyName("img")]
    public WolframImage? Image { get; set; }
}

/// <summary>
/// Image data from a Wolfram|Alpha subpod. MIME type is provided inline
/// via <c>contenttype</c> so no HTTP header parsing is needed.
/// </summary>
public sealed class WolframImage
{
    [JsonPropertyName("src")]
    public string Src { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("alt")]
    public string? Alt { get; set; }

    [JsonPropertyName("contenttype")]
    public string? ContentType { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

/// <summary>
/// Disambiguation assumption proposed by Wolfram|Alpha. The model re-queries
/// with <see cref="AssumptionValue.Input"/> as the <c>assumption</c> parameter.
/// </summary>
public sealed class Assumption
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("word")]
    public string? Word { get; set; }

    [JsonPropertyName("values")]
    public List<AssumptionValue> Values { get; set; } = [];
}

public sealed class AssumptionValue
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = "";

    [JsonPropertyName("input")]
    public string Input { get; set; } = "";
}

public sealed class DidYouMean
{
    [JsonPropertyName("val")]
    public string Val { get; set; } = "";

    [JsonPropertyName("score")]
    public string? Score { get; set; }
}

public sealed class Tip
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

/// <summary>
/// Pod state transition (e.g. "More details", "Step-by-step solution").
/// Pass <see cref="Input"/> as <c>podstate</c> to expand.
/// </summary>
public sealed class PodState
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("input")]
    public string Input { get; set; } = "";
}
