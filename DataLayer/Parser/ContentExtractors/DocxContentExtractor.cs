using System.Text;
using System.Threading;
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

        public string GetContent(string filePath, CancellationToken token)
        {
            var doc = DocX.Load(filePath);
            var builder = new StringBuilder();
            foreach(var paragraph in doc.Paragraphs)
            {
                token.ThrowIfCancellationRequested();
                builder.AppendLine(paragraph.Text);
            }
            return builder.ToString();
        }
    }
}
