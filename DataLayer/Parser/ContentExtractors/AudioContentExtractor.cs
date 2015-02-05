using System;
using System.Text;
using Common.Utils;
using DataLayer.Model;
using DataLayer.Parser.ContentExtractors.Base;
using TagLib;

namespace DataLayer.Parser.ContentExtractors
{
    public class AudioContentExtractor : IContentExtractor
    {
        public DocumentType[] SupporterTypes
        {
            get { return new[] { DocumentType.Audio }; }
        }

        public string GetContent(string filePath)
        {
            Tag tag = null;
            try
            {
                tag = File.Create(filePath).Tag;
            }
            catch(Exception e)
            {
                Logger.Instance.Warn("Не удалось получить информацию о треке '{0}': {1}", filePath, (object)e);
            }

            if(tag == null)
                return String.Empty;

            var builder = new StringBuilder();
            if(tag.Performers != null)
            {
                foreach(var performer in tag.Performers)
                {
                    builder.Append(performer);
                    builder.Append(" ");
                }

                builder.AppendLine();
            }

            if(tag.Album != null)
            {
                builder.AppendLine(tag.Album);
            }

            if(tag.Title != null)
            {
                builder.AppendLine(tag.Title);
            }

            if(tag.Lyrics != null)
            {
                builder.AppendLine(tag.Lyrics);
            }

            return builder.ToString();
        }
    }
}