using System.Reflection;
using System.Text;
using DataLayer.Model;
using DataLayer.Parser.ContentExtractors.Base;
using Microsoft.Office.Interop.Word;

namespace DataLayer.Parser.ContentExtractors
{
    public class DocRtfContentExtractor : IContentExtractor
    {
        public DocumentType[] SupporterTypes
        {
            get { return new[] {DocumentType.Doc, DocumentType.Rtf}; }
        }

        public string GetContent(string filePath)
        {
            var word = new Application();
            object miss = Missing.Value;
            object path = filePath;
            object readOnly = true;
            var docs = word.Documents.Open(ref path, ref miss, ref readOnly, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss);

            var builder = new StringBuilder();
            for(var i = 0; i < docs.Paragraphs.Count; i++)
            {
                builder.AppendLine(docs.Paragraphs[i + 1].Range.Text);
            }

            docs.Close();
            word.Quit();

            return builder.ToString();
        }
    }
}