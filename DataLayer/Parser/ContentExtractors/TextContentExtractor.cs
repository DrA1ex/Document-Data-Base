using System.IO;
using System.Text;
using DataLayer.Model;
using DataLayer.Parser.ContentExtractors.Base;
using Ude;

namespace DataLayer.Parser.ContentExtractors
{
    public class TextContentExtractor : IContentExtractor
    {
        public DocumentType[] SupporterTypes
        {
            get { return new[] {DocumentType.Text}; }
        }

        public string GetContent(string filePath)
        {
            using(var fs = File.OpenRead(filePath))
            {
                var cdet = new CharsetDetector();
                cdet.Feed(fs);
                cdet.DataEnd();

                var encoding = Encoding.Default;
                if(cdet.Charset != null)
                {
                    encoding = Encoding.GetEncoding(cdet.Charset);
                }

                fs.Seek(0, SeekOrigin.Begin);

                using(var reader = new StreamReader(fs, encoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}