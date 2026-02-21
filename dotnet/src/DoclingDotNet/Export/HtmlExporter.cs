using System.Text;
using System.Linq;
using System.Net;
using DoclingDotNet.Pipeline;

namespace DoclingDotNet.Export;

public static class HtmlExporter
{
    public static string Export(PdfConversionRunResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine($"  <title>{WebUtility.HtmlEncode(result.DocumentKey)}</title>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        
        foreach (var page in result.Pages)
        {
            foreach (var cell in page.TextlineCells)
            {
                var text = WebUtility.HtmlEncode(cell.Text);
                if (string.IsNullOrWhiteSpace(text)) continue;

                if (cell.FontName != null && cell.FontName.Contains("Heading", System.StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"  <h1>{text}</h1>");
                }
                else
                {
                    sb.AppendLine($"  <p>{text}</p>");
                }
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        
        return sb.ToString().TrimEnd();
    }
}
