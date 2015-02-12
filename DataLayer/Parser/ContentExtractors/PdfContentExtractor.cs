using System.Text;
using System.Threading;
using DataLayer.Model;
using DataLayer.Parser.ContentExtractors.Base;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;

namespace DataLayer.Parser.ContentExtractors
{
    public class PdfContentExtractor : IContentExtractor
    {
        public DocumentType[] SupporterTypes
        {
            get { return new[] { DocumentType.Pdf }; }
        }

        public string GetContent(string filePath, CancellationToken token)
        {
            using(var reader = new PdfReader(filePath))
            {
                var result = new StringBuilder();
                for(int i = 1; i <= reader.NumberOfPages; i++)
                {
                    token.ThrowIfCancellationRequested();
                    result.AppendLine(PdfTextExtractor.GetTextFromPage(reader, i));
                }


                return result.ToString();
            }
        }
    }
}