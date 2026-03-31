using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class RunJavaScriptToolTests
{
    [TestMethod]
    public void MathExpression()
    {
        var json = RunJavaScriptTool.RunJavaScript("Math.sqrt(144) + Math.pow(2, 10)");
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("result").GetDouble();
        Assert.AreEqual(1036.0, result);
    }

    [TestMethod]
    public void JsonProcessing()
    {
        var json = RunJavaScriptTool.RunJavaScript(
            "JSON.parse('{\"a\":1,\"b\":2}')");
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("result");
        Assert.AreEqual(1, result.GetProperty("a").GetInt32());
        Assert.AreEqual(2, result.GetProperty("b").GetInt32());
    }

    [TestMethod]
    public void ArrayOperations()
    {
        var json = RunJavaScriptTool.RunJavaScript(
            "[1,2,3,4,5].filter(x => x > 3)");
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("result");
        Assert.AreEqual(JsonValueKind.Array, arr.ValueKind);
        Assert.AreEqual(2, arr.GetArrayLength());
    }

    [TestMethod]
    public void VariablesInjection()
    {
        var json = RunJavaScriptTool.RunJavaScript(
            "data.items.filter(x => x.price > 100).map(x => x.name)",
            variables: "{\"data\": {\"items\": [{\"name\": \"A\", \"price\": 50}, {\"name\": \"B\", \"price\": 200}]}}");
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("result");
        Assert.AreEqual(1, arr.GetArrayLength());
        Assert.AreEqual("B", arr[0].GetString());
    }

    [TestMethod]
    public void ConsoleLogCapture()
    {
        var json = RunJavaScriptTool.RunJavaScript("console.log('hello', 'world'); 42");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(42, doc.RootElement.GetProperty("result").GetInt64());
        Assert.IsTrue(doc.RootElement.TryGetProperty("console_output", out var output));
        Assert.IsTrue(output.GetString()!.Contains("hello world"));
    }

    [TestMethod]
    public void TimeoutOrStatementLimitOnInfiniteLoop()
    {
        var json = RunJavaScriptTool.RunJavaScript("while(true){}", timeout_seconds: 1);
        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString()!;
        Assert.IsTrue(error.Contains("timed out") || error.Contains("Timeout")
            || error.Contains("Statement") || error.Contains("limit"),
            $"Expected timeout or statement limit error, got: {error}");
    }

    [TestMethod]
    public void MaxStatementsExceeded()
    {
        var json = RunJavaScriptTool.RunJavaScript(
            "var i = 0; while(i < 200000) { i++; }", timeout_seconds: 30);
        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString()!;
        Assert.IsTrue(error.Contains("Statement") || error.Contains("limit") || error.Contains("timed out"),
            $"Expected statement limit error, got: {error}");
    }

    [TestMethod]
    public void InvalidVariablesJson()
    {
        var json = RunJavaScriptTool.RunJavaScript("1+1", variables: "not json");
        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error").GetString()!;
        Assert.IsTrue(error.Contains("Invalid variables JSON"));
    }

    [TestMethod]
    public void DateSupport()
    {
        var json = RunJavaScriptTool.RunJavaScript("new Date().getFullYear()");
        using var doc = JsonDocument.Parse(json);
        var year = doc.RootElement.GetProperty("result").GetInt64();
        Assert.AreEqual(DateTime.Now.Year, (int)year);
    }

    [TestMethod]
    public void RegexSupport()
    {
        var json = RunJavaScriptTool.RunJavaScript("'hello123world'.match(/\\d+/)[0]");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual("123", doc.RootElement.GetProperty("result").GetString());
    }
}
