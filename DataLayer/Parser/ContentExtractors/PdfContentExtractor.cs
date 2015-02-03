using System.Text;
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

        public string GetContent(string filePath)
        {
            using(var reader = new PdfReader(filePath))
            {
                var result = new StringBuilder();
                for(int i = 1; i <= reader.NumberOfPages; i++)
                {
                    result.AppendLine(PdfTextExtractor.GetTextFromPage(reader, i));
                }


                return result.ToString();
            }
        }
    }
}