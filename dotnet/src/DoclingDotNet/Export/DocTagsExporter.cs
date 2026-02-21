using System.Text;
using System.Linq;
using System.Net;
using DoclingDotNet.Pipeline;

namespace DoclingDotNet.Export;

public static class DocTagsExporter
{
    public static string Export(PdfConversionRunResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<document>");
        
        for (int i = 0; i < result.Pages.Count; i++)
        {
            var page = result.Pages[i];
            sb.AppendLine($"  <page num=\"{i + 1}\" width=\"{page.Dimension.Rect.RX2}\" height=\"{page.Dimension.Rect.RY2}\">");
            
            foreach (var cell in page.TextlineCells)
            {
                var text = WebUtility.HtmlEncode(cell.Text);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var rect = cell.Rect;
                sb.AppendLine($"    <text bbox=\"{rect.RX0},{rect.RY0},{rect.RX2},{rect.RY2}\">{text}</text>");
            }
            
            sb.AppendLine("  </page>");
        }

        sb.AppendLine("</document>");
        
        return sb.ToString().TrimEnd();
    }
}
