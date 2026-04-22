using System.Net;
using System.Text;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Services.WolframAlpha;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class WolframAlphaResultConverterTests
{
    [TestMethod]
    public async Task ConvertAsync_SuccessWithTextAndMathML_EmitsTextContentIncludingMathML()
    {
        var qr = new QueryResult
        {
            Success = true,
            InputString = "integrate x^2 dx",
            Pods =
            [
                new Pod
                {
                    Title = "Indefinite integral",
                    Id = "IndefiniteIntegral",
                    SubPods =
                    [
                        new SubPod
                        {
                            Plaintext = "integral x^2 dx = x^3/3 + constant",
                            MathML = "<math xmlns='http://www.w3.org/1998/Math/MathML'><mn>1</mn></math>",
                        },
                    ],
                },
            ],
        };

        var converter = new ResultConverter(NoNetworkClient());
        var blocks = await converter.ConvertAsync(qr, CancellationToken.None);

        // Pod text + source link — no images for non-visual pod with plaintext.
        Assert.AreEqual(2, blocks.Count);
        var first = (TextContentBlock)blocks[0];
        StringAssert.Contains(first.Text, "### Indefinite integral");
        StringAssert.Contains(first.Text, "integral x^2 dx = x^3/3 + constant");
        StringAssert.Contains(first.Text, "<math xmlns=");

        var link = (TextContentBlock)blocks[1];
        StringAssert.Contains(link.Text, "wolframalpha.com/input?i=integrate");
    }

    [TestMethod]
    public async Task ConvertAsync_VisualPod_FetchesImageUsingJsonMime()
    {
        var qr = new QueryResult
        {
            Success = true,
            InputString = "plot sin(x)",
            Pods =
            [
                new Pod
                {
                    Title = "Plot",
                    Id = "Plot",
                    SubPods =
                    [
                        new SubPod
                        {
                            Plaintext = "",
                            Image = new WolframImage
                            {
                                Src = "https://public6.wolframalpha.com/files/plot.gif",
                                ContentType = "image/gif",
                            },
                        },
                    ],
                },
            ],
        };

        var http = new HttpClient(new StubHandler(new byte[] { 0x47, 0x49, 0x46 }));
        var converter = new ResultConverter(http);
        var blocks = await converter.ConvertAsync(qr, CancellationToken.None);

        // Pod text + image + source link
        Assert.AreEqual(3, blocks.Count);
        var img = (ImageContentBlock)blocks[1];
        Assert.AreEqual("image/gif", img.MimeType);
    }

    [TestMethod]
    public async Task ConvertAsync_NonVisualPodWithoutPlaintext_StillFetchesImage()
    {
        var qr = new QueryResult
        {
            Success = true,
            InputString = "foo",
            Pods =
            [
                new Pod
                {
                    Title = "Obscure",
                    Id = "SomeUnknownPod",
                    SubPods =
                    [
                        new SubPod
                        {
                            Plaintext = "",
                            Image = new WolframImage
                            {
                                Src = "https://public6.wolframalpha.com/files/x.gif",
                                ContentType = "image/png",
                            },
                        },
                    ],
                },
            ],
        };

        var converter = new ResultConverter(new HttpClient(new StubHandler(new byte[] { 0x89, 0x50, 0x4E, 0x47 })));
        var blocks = await converter.ConvertAsync(qr, CancellationToken.None);

        Assert.AreEqual(3, blocks.Count);
        var img = (ImageContentBlock)blocks[1];
        Assert.AreEqual("image/png", img.MimeType);
    }

    [TestMethod]
    public async Task ConvertAsync_ImageFetchFails_SkipsImageGracefully()
    {
        var qr = new QueryResult
        {
            Success = true,
            InputString = "foo",
            Pods =
            [
                new Pod
                {
                    Title = "Plot",
                    Id = "Plot",
                    SubPods = [new SubPod { Image = new WolframImage { Src = "https://x/y.gif", ContentType = "image/gif" } }],
                },
            ],
        };

        var converter = new ResultConverter(new HttpClient(new StubHandler(statusCode: HttpStatusCode.NotFound)));
        var blocks = await converter.ConvertAsync(qr, CancellationToken.None);

        // Pod text + source link; no image because fetch failed.
        Assert.AreEqual(2, blocks.Count);
        Assert.IsFalse(blocks.Any(b => b is ImageContentBlock));
    }

    [TestMethod]
    public async Task ConvertAsync_WithAssumptions_EmitsDisambiguationBlock()
    {
        var qr = new QueryResult
        {
            Success = true,
            InputString = "pi",
            Pods = [],
            Assumptions =
            [
                new Assumption
                {
                    Type = "Clash",
                    Word = "pi",
                    Values =
                    [
                        new AssumptionValue { Name = "MathConst", Desc = "the mathematical constant", Input = "*C.pi-_*MathematicalConstant-" },
                        new AssumptionValue { Name = "Movie",     Desc = "the movie",                   Input = "*C.pi-_*Movie-" },
                    ],
                },
            ],
        };

        var converter = new ResultConverter(NoNetworkClient());
        var blocks = await converter.ConvertAsync(qr, CancellationToken.None);

        var disambig = blocks.OfType<TextContentBlock>().FirstOrDefault(b => b.Text.Contains("Disambiguation Required"));
        Assert.IsNotNull(disambig);
        StringAssert.Contains(disambig.Text, "*C.pi-_*MathematicalConstant-");
        StringAssert.Contains(disambig.Text, "the movie");
    }

    [TestMethod]
    public async Task ConvertAsync_FailureWithTipsAndDidYouMeans_EmitsErrorBlockInPriorityOrder()
    {
        var qr = new QueryResult
        {
            Success = false,
            InputString = "asdfghjkl",
            Tips = [new Tip { Text = "Check spelling." }],
            DidYouMeans = [new DidYouMean { Val = "asdf" }],
        };

        var converter = new ResultConverter(NoNetworkClient());
        var blocks = await converter.ConvertAsync(qr, CancellationToken.None);

        Assert.AreEqual(1, blocks.Count);
        var err = (TextContentBlock)blocks[0];
        StringAssert.Contains(err.Text, "could not interpret");
        StringAssert.Contains(err.Text, "Tip: Check spelling.");
        StringAssert.Contains(err.Text, "Possible alternatives");
        StringAssert.Contains(err.Text, "asdf");
    }

    [TestMethod]
    public async Task ConvertAsync_RealFixture_IntegrateXSquared_ParsesEndToEnd()
    {
        const string fixture = """
        {
          "queryresult": {
            "success": true,
            "error": false,
            "numpods": 1,
            "inputstring": "integrate x^2 dx",
            "timing": 0.584,
            "pods": [
              {
                "title": "Indefinite integral",
                "id": "IndefiniteIntegral",
                "position": 10,
                "primary": true,
                "scanner": "Integral",
                "subpods": [
                  {
                    "title": "",
                    "plaintext": "integral x^2 dx = x^3/3 + constant",
                    "mathml": "<math xmlns='http://www.w3.org/1998/Math/MathML'><mi>x</mi></math>",
                    "img": { "src": "https://x/y.gif", "contenttype": "image/gif", "width": 162, "height": 36 }
                  }
                ]
              }
            ]
          }
        }
        """;

        var parsed = JsonSerializer.Deserialize<WolframResponse>(fixture)!;
        Assert.IsTrue(parsed.QueryResult.Success);
        Assert.AreEqual("integrate x^2 dx", parsed.QueryResult.InputString);
        Assert.AreEqual(1, parsed.QueryResult.Pods!.Count);
        Assert.AreEqual("image/gif", parsed.QueryResult.Pods![0].SubPods[0].Image!.ContentType);

        // Non-visual pod (IndefiniteIntegral) with plaintext → image should be skipped.
        var converter = new ResultConverter(new HttpClient(new StubHandler(failIfCalled: true)));
        var blocks = await converter.ConvertAsync(parsed.QueryResult, CancellationToken.None);

        Assert.AreEqual(2, blocks.Count);
        Assert.IsFalse(blocks.Any(b => b is ImageContentBlock));
    }

    static HttpClient NoNetworkClient() => new(new StubHandler(failIfCalled: true));

    sealed class StubHandler : HttpMessageHandler
    {
        readonly byte[]? _bytes;
        readonly HttpStatusCode _status;
        readonly bool _failIfCalled;

        public StubHandler(byte[] bytes) { _bytes = bytes; _status = HttpStatusCode.OK; }
        public StubHandler(HttpStatusCode statusCode) { _status = statusCode; }
        public StubHandler(bool failIfCalled) { _failIfCalled = failIfCalled; _status = HttpStatusCode.OK; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_failIfCalled)
                throw new InvalidOperationException("HTTP should not have been called.");

            var response = new HttpResponseMessage(_status);
            if (_bytes is not null)
                response.Content = new ByteArrayContent(_bytes);
            return Task.FromResult(response);
        }
    }
}
