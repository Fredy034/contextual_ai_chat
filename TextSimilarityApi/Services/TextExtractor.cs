using DocumentFormat.OpenXml.Packaging;
using ExcelDataReader;              // para .xls/.xlsx
using NPOI.XWPF;                    // para .doc (HWPF)
using NPOI.XWPF.Extractor;          // WordExtractor
using NPOI.XWPF.UserModel;
using Tesseract;                    // para OCR en imágenes
using UglyToad.PdfPig;              // para .pdf
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TextSimilarityApi.Services
{
    public class TextExtractor
    {
        public TextExtractor()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public async Task<string> ExtractTextAsync(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".txt":
                        return await File.ReadAllTextAsync(filePath);

                    case ".pdf":
                        return ExtractTextFromPdf(filePath);

                    case ".docx":
                        return ExtractTextFromDocx(filePath);

                    case ".doc":
                        return ExtractTextFromDoc(filePath);

                    case ".xlsx":
                    case ".xls":
                        return ExtractTextFromExcel(filePath);

                    case ".csv":
                        return await File.ReadAllTextAsync(filePath);

                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".gif":
                    case ".bmp":
                    case ".tiff":
                        //return await _azureOcrService.ReadTextFromFileAsync(filePath, "es");
                        return ExtractTextFromImageTesseract(filePath);

                    default:
                        throw new NotSupportedException($"Formato no soportado: {extension}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextExtractor] Error extrayendo '{filePath}': {ex.Message}");
                throw;
            }
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

        private static string ExtractTextFromDocx(string filePath)
        {
            // DocumentFormat.OpenXml para .docx
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            return body?.InnerText ?? string.Empty;
        }

        private static string ExtractTextFromDoc(string filePath)
        {
            // NPOI HWPF para .doc (formato binario)
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var doc = new XWPFDocument(stream);
            var extractor = new XWPFWordExtractor(doc);
            var text = extractor.Text;
            return text ?? string.Empty;
        }

        private static string ExtractTextFromExcel(string filePath)
        {
            // ExcelDataReader: creamos un reader y recorremos filas/columnas concatenando valores.
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var sb = new StringBuilder();
            do
            {
                while (reader.Read())
                {
                    var values = new List<string>();
                    for (int c = 0; c < reader.FieldCount; c++)
                    {
                        var cell = reader.GetValue(c);
                        if (cell != null)
                        {
                            var s = cell.ToString();
                            s = Regex.Replace(s, @"\r\n?|\n", " ");
                            s = s.Trim();
                            if (!string.IsNullOrEmpty(s))
                                values.Add(s);
                        }
                    }
                    if (values.Count > 0)
                        sb.AppendLine(string.Join("\t", values));
                }
            } while (reader.NextResult());

            var result = sb.ToString();

            return result;
        }

        private static string ExtractTextFromImageTesseract(string filePath)
        {
            var thisPath = AppContext.BaseDirectory; // normalmente bin/<...>
            var tessDataPath = Path.Combine(thisPath, "tessdata");

            if (!Directory.Exists(tessDataPath))
            {
                tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
            }

            var languages = "spa+eng";

            try
            {
                using var engine = new TesseractEngine(tessDataPath, languages, EngineMode.Default);
                using var img = Pix.LoadFromFile(filePath);
                using var page = engine.Process(img);
                var ocrText = page.GetText() ?? string.Empty;
                var meanConfidence = page.GetMeanConfidence();

                var snippet = ocrText.Length > 500 ? ocrText.Substring(0, 500) + "..." : ocrText;

                var sb = new StringBuilder();
                sb.AppendLine("[OCR extraído de imagen]");
                sb.AppendLine(snippet);
                sb.AppendLine();
                sb.AppendLine($"[OCR Confidence: {meanConfidence:0.00}]");
                sb.AppendLine($"[Imagen: {Path.GetFileName(filePath)}, Tamaño: {new FileInfo(filePath).Length / 1024} KB]");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TextExtractor][OCR] Error: {ex}");
                throw;
            }
        }

        public async Task<string> ExtractTextFromImageFileAsync(string filePath)
        {
            return await Task.Run(() => ExtractTextFromImageTesseract(filePath));
        }
    }
}
