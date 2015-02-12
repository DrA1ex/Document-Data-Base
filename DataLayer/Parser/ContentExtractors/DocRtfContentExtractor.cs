using System;
using System.Reflection;
using System.Text;
using System.Threading;
using DataLayer.Model;
using DataLayer.Parser.ContentExtractors.Base;
using Microsoft.Office.Interop.Word;

namespace DataLayer.Parser.ContentExtractors
{
    public class DocRtfContentExtractor : IContentExtractor
    {
        public DocumentType[] SupporterTypes
        {
            get { return new[] { DocumentType.Doc, DocumentType.Rtf }; }
        }

        public string GetContent(string filePath, CancellationToken token)
        {
            var word = new Application();
            object miss = Missing.Value;
            object path = filePath;
            object readOnly = true;
            var docs = word.Documents.Open(ref path, ref miss, ref readOnly, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss, ref miss);

            var builder = new StringBuilder();
            try
            {
                for(var i = 0; i < docs.Paragraphs.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    builder.AppendLine(docs.Paragraphs[i + 1].Range.Text);
                }
            }
            finally
            {
                docs.Close();
                word.Quit();
            }

            return builder.ToString();
        }
    }
}