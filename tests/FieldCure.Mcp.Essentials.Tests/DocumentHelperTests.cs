using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class DocumentHelperTests
{
    [TestMethod]
    [DataRow(".pdf", true)]
    [DataRow(".docx", true)]
    [DataRow(".hwpx", true)]
    [DataRow(".pptx", true)]
    [DataRow(".xlsx", true)]
    [DataRow(".PDF", true)]
    [DataRow(".DOCX", true)]
    [DataRow(".txt", false)]
    [DataRow(".cs", false)]
    [DataRow(".json", false)]
    [DataRow(".html", false)]
    [DataRow("", false)]
    public void IsBinaryDocument(string extension, bool expected)
    {
        Assert.AreEqual(expected, DocumentHelper.IsBinaryDocument(extension));
    }

    [TestMethod]
    [DataRow("application/pdf", ".pdf")]
    [DataRow("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx")]
    [DataRow("application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx")]
    [DataRow("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx")]
    [DataRow("text/html", null)]
    [DataRow("application/json", null)]
    [DataRow(null, null)]
    public void ContentTypeToExtension(string? contentType, string? expected)
    {
        Assert.AreEqual(expected, DocumentHelper.ContentTypeToExtension(contentType));
    }

    [TestMethod]
    [DataRow("https://example.com/report.pdf", ".pdf")]
    [DataRow("https://example.com/doc.docx", ".docx")]
    [DataRow("https://example.com/slides.pptx", ".pptx")]
    [DataRow("https://example.com/data.xlsx", ".xlsx")]
    [DataRow("https://example.com/document.hwpx", ".hwpx")]
    [DataRow("https://example.com/report.PDF", ".PDF")]
    [DataRow("https://example.com/page.html", null)]
    [DataRow("https://example.com/api/data", null)]
    [DataRow("https://example.com/", null)]
    [DataRow("not-a-url", null)]
    public void UrlToExtension(string url, string? expected)
    {
        Assert.AreEqual(expected, DocumentHelper.UrlToExtension(url));
    }

    [TestMethod]
    public void ParseUnsupportedExtensionThrows()
    {
        Assert.ThrowsExactly<NotSupportedException>(
            () => DocumentHelper.Parse([], ".xyz"));
    }
}
