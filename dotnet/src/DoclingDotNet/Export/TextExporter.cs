using System.Text;
using System.Linq;
using DoclingDotNet.Pipeline;

namespace DoclingDotNet.Export;

public static class TextExporter
{
    public static string Export(PdfConversionRunResult result)
    {
        var sb = new StringBuilder();
        
        foreach (var page in result.Pages)
        {
            foreach (var cell in page.TextlineCells)
            {
                var text = cell.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}
