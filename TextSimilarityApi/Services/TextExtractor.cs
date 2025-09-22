using DocumentFormat.OpenXml.Packaging;
using ExcelDataReader;              // para .xls/.xlsx
using NPOI.XWPF;                    // para .doc (HWPF)
using NPOI.XWPF.Extractor;          // WordExtractor
using NPOI.XWPF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Util;

namespace TextSimilarityApi.Services
{
    public static class TextExtractor
    {
        // Registrar proveedor de codificaciones requerido por ExcelDataReader (para formatos antiguos)
        static TextExtractor()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public static string ExtractText(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                return extension switch
                {
                    ".txt" => File.ReadAllText(filePath),
                    ".pdf" => ExtractTextFromPdf(filePath),
                    ".docx" => ExtractTextFromDocx(filePath),
                    ".doc" => ExtractTextFromDoc(filePath),
                    ".xlsx" => ExtractTextFromExcel(filePath),
                    ".xls" => ExtractTextFromExcel(filePath),
                    ".csv" => ExtractTextFromCsv(filePath),
                    _ => throw new NotSupportedException($"Formato no soportado: {extension}")
                };
            }
            catch (Exception ex)
            {
                // Loguea para diagnóstico (puedes reemplazar con ILogger si lo prefieres)
                Console.WriteLine($"[TextExtractor] Error extrayendo '{filePath}': {ex.Message}");
                throw; // re-lanzar para que el controlador decida cómo manejarlo
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

        private static string ExtractTextFromCsv(string filePath)
        {
            // Leer todo el CSV (si quieres más control usa CsvHelper)
            return File.ReadAllText(filePath);
        }

        private static string ExtractTextFromExcel(string filePath)
        {
            // ExcelDataReader: creamos un reader y recorremos filas/columnas concatenando valores.
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var sb = new StringBuilder();
            // Recorremos todas las hojas
            do
            {
                // recorrer filas
                while (reader.Read())
                {
                    // reader.FieldCount puede ser 0 en algunas filas; iterar columnas válidas
                    var values = new List<string>();
                    for (int c = 0; c < reader.FieldCount; c++)
                    {
                        var cell = reader.GetValue(c);
                        if (cell != null)
                        {
                            var s = cell.ToString();
                            // Limpiar espacios excesivos y saltos de línea dentro de celdas
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

            // Opcional: truncar si es muy largo (pero en general querrás todo el texto)
            return result;
        }
    }
}
