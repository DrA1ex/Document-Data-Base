using DataLayer.Model;

namespace DataLayer.Parser.ContentExtractors.Base
{
    interface IContentExtractor
    {
        DocumentType SupporterType { get; }

        string GetContent(string filePath);
    }
}
