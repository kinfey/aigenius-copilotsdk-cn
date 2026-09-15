using Ganss.Xss;
using Markdig;

namespace AgentHQDemo.Web.Services;

public static class MarkdownContent
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .DisableHtml()
        .Build();

    public static string Render(string markdown)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(
            ["p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "em",
             "del", "s", "blockquote", "ul", "ol", "li", "pre", "code", "a",
             "table", "thead", "tbody", "tr", "th", "td"]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "title", "start", "colspan", "rowspan"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);

        var html = sanitizer.Sanitize(Markdown.ToHtml(markdown, Pipeline));
        return html.Replace("<table>", "<div class=\"markdown-table-wrap\"><table>", StringComparison.Ordinal)
            .Replace("</table>", "</table></div>", StringComparison.Ordinal);
    }
}
