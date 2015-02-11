using System.Threading;
using DataLayer.Model;

namespace DataLayer.Parser.ContentExtractors.Base
{
    interface IContentExtractor
    {
        DocumentType[] SupporterTypes { get; }

        string GetContent(string filePath, CancellationToken token);
    }
}
