using UglyToad.PdfPig;
using System.Text;

namespace TextSimilarityApi.Services
{
    public static class TextExtractor
    {
        public static string ExtractText(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".txt" => File.ReadAllText(filePath),
                ".pdf" => ExtractTextFromPdf(filePath),
                _ => throw new NotSupportedException($"Formato no soportado: {extension}")
            };
        }

        private static string ExtractTextFromPdf(string filePath)
        {
            var sb = new StringBuilder();

            using var document = PdfDocument.Open(filePath);
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            return sb.ToString();
        }
    }
}
