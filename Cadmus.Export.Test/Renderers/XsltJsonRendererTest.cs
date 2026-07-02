using Cadmus.Export.Filters;
using Cadmus.Export.Renderers;
using Cadmus.General.Parts;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Cadmus.Export.Test.Renderers;

public sealed class XsltJsonRendererTest
{
    private readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    [Fact]
    public void Render_XsltOnly_Ok()
    {
        XsltJsonRenderer renderer = new();
        renderer.Configure(new XsltJsonRendererOptions
        {
            Xslt = TestHelper.LoadResourceText("TokenTextPart.xslt")
        });
        TokenTextPart text = CadmusPreviewerTest.GetSampleTextPart();
        string json = JsonSerializer.Serialize(text, _options);

        string result = renderer.Render(json, new CadmusRendererContext());

        Assert.NotNull(result);
        Assert.Equal("[CIL 1,23]\r\n1  que bixit\r\n2  annos XX\r\n", result);
    }

    [Fact]
    public void Render_XsltOnlyWithArrayWrap_Ok()
    {
        XsltJsonRenderer renderer = new();
        renderer.Configure(new XsltJsonRendererOptions
        {
            Xslt = TestHelper.LoadResourceText("TokenTextPartWrap.xslt"),
            WrappedEntryNames = new Dictionary<string, string>
            {
                ["lines"] = "line"
            }
        });
        TokenTextPart text = CadmusPreviewerTest.GetSampleTextPart();
        string json = JsonSerializer.Serialize(text, _options);

        string result = renderer.Render(json, new CadmusRendererContext());

        Assert.NotNull(result);
        Assert.Equal("[CIL 1,23]\r\n1  que bixit\r\n2  annos XX\r\n", result);
    }

    [Fact]
    public void Render_JmesPathOnly_Ok()
    {
        XsltJsonRenderer renderer = new();
        renderer.Configure(new XsltJsonRendererOptions
        {
            JsonExpressions = ["root.citation"],
            QuoteStripping = true
        });
        TokenTextPart text = CadmusPreviewerTest.GetSampleTextPart();
        string json = JsonSerializer.Serialize(text, _options);

        string result = renderer.Render(json, new CadmusRendererContext());

        Assert.NotNull(result);
        Assert.Equal("CIL 1,23", result);
    }

    [Fact]
    public void Render_JmesPathOnlyMd_Ok()
    {
        XsltJsonRenderer renderer = new();
        renderer.Configure(new XsltJsonRendererOptions
        {
            JsonExpressions = ["root.text"],
            QuoteStripping = true,
        });
        MarkdownTextFilter filter = new();
        filter.Configure(new MarkdownRendererFilterOptions
        {
            Format = "html"
        });
        renderer.Filters.Add(filter);

        NotePart note = new()
        {
            CreatorId = "zeus",
            UserId = "zeus",
            Text = "This is a *note* using MD"
        };
        string json = JsonSerializer.Serialize(note, _options);

        string result = renderer.Render(json, new CadmusRendererContext());

        Assert.NotNull(result);
        Assert.Equal("<p>This is a <em>note</em> using MD</p>\n", result);
    }
}
