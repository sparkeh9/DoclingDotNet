using System.Text;
using System.Linq;
using DoclingDotNet.Pipeline;

namespace DoclingDotNet.Export;

public static class MarkdownExporter
{
    public static string Export(PdfConversionRunResult result)
    {
        var sb = new StringBuilder();
        
        foreach (var page in result.Pages)
        {
            foreach (var cell in page.TextlineCells)
            {
                var text = cell.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                if (cell.FontName != null && cell.FontName.Contains("Heading", System.StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"# {text}");
                }
                else
                {
                    sb.AppendLine(text);
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
