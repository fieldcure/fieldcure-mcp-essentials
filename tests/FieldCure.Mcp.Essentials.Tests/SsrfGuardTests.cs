using FieldCure.Mcp.Essentials.Http;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class SsrfGuardTests
{
    [TestMethod]
    public void ValidateUrl_AcceptsHttp()
    {
        var (uri, error) = SsrfGuard.ValidateUrl("http://example.com");
        Assert.IsNotNull(uri);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void ValidateUrl_AcceptsHttps()
    {
        var (uri, error) = SsrfGuard.ValidateUrl("https://example.com");
        Assert.IsNotNull(uri);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void ValidateUrl_RejectsFtp()
    {
        var (uri, error) = SsrfGuard.ValidateUrl("ftp://example.com");
        Assert.IsNull(uri);
        Assert.IsNotNull(error);
        Assert.IsTrue(error!.Contains("Only http"));
    }

    [TestMethod]
    public void ValidateUrl_RejectsGarbage()
    {
        var (uri, error) = SsrfGuard.ValidateUrl("not-a-url");
        Assert.IsNull(uri);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public async Task CheckAsync_BlocksLocalhost()
    {
        var uri = new Uri("http://localhost:9999");
        var result = await SsrfGuard.CheckAsync(uri, CancellationToken.None);
        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Contains("SSRF"));
    }

    [TestMethod]
    public async Task CheckAsync_BlocksPrivateIp()
    {
        var uri = new Uri("http://192.168.1.1");
        var result = await SsrfGuard.CheckAsync(uri, CancellationToken.None);
        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Contains("SSRF"));
    }

    [TestMethod]
    public async Task CheckAsync_AllowsPublicHost()
    {
        var uri = new Uri("https://example.com");
        var result = await SsrfGuard.CheckAsync(uri, CancellationToken.None);
        Assert.IsNull(result);
    }
}
