using AgentHQDemo.Web.Services;

namespace AgentHQDemo.Tests;

public sealed class MarkdownContentTests
{
    [Fact]
    public void RendersCommonMarkdownAndTables()
    {
        var html = MarkdownContent.Render("""
            ## Results

            **Total** and *average* with ~~old~~.

            - Retail
            - `C003`

            > Verified

            | Customer | Total |
            | --- | --- |
            | C003 | 1700 |

            [Docs](https://learn.microsoft.com/)
            """);
        foreach (var expected in new[] { "<h2>", "<strong>", "<em>", "<del>", "<ul>", "<li>",
                     "<code>C003</code>", "<blockquote>", "<table>", "<th>", "<td>",
                     "class=\"markdown-table-wrap\"", "href=\"https://learn.microsoft.com/\"" })
            Assert.Contains(expected, html);
    }

    [Fact]
    public void CodeBlocksRemainLiteralAndPreserveWhitespace()
    {
        var html = MarkdownContent.Render("```html\n<script>alert(1)</script>\n  indented\n```");
        Assert.Contains("<pre><code>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("\n  indented\n", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Theory]
    [InlineData("[click](javascript:alert%281%29)")]
    [InlineData("[click](data:text/html,test)")]
    [InlineData("[click](vbscript:msgbox%281%29)")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<svg onload=alert(1)></svg>")]
    [InlineData("![tracking](https://example.com/track.png)")]
    public void DoesNotEmitExecutableMarkupOrRemoteImages(string markdown)
    {
        var html = MarkdownContent.Render(markdown);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<svg", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"vbscript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PartialStreamingMarkdownCanBeRenderedAndCompleted()
    {
        const string markdown = "**Total**\n\n```csharp\nvar total = 1700;\n```\n";
        for (var length = 0; length <= markdown.Length; length++)
            Assert.NotNull(MarkdownContent.Render(markdown[..length]));
        var complete = MarkdownContent.Render(markdown);
        Assert.Contains("<strong>Total</strong>", complete);
        Assert.Contains("<pre><code>", complete);
        Assert.Contains("1700", complete);
    }
}
