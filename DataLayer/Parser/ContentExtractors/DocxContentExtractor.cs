using System.Text;
using DataLayer.Model;
using DataLayer.Parser.ContentExtractors.Base;
using Novacode;


namespace DataLayer.Parser.ContentExtractors
{
    public class DocxContentExtractor : IContentExtractor
    {
        public DocumentType[] SupporterTypes
        {
            get { return new[] { DocumentType.DocX }; }
        }

        public string GetContent(string filePath)
        {
            var doc = DocX.Load(filePath);
            var builder = new StringBuilder();
            foreach(var paragraph in doc.Paragraphs)
            {
                builder.AppendLine(paragraph.Text);
            }
            return builder.ToString();
        }
    }
}
