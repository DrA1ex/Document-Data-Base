﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Common.Utils;
using DataLayer.Model;
using DataLayer.Parser.ContentExtractors.Base;

namespace DataLayer.Parser
{
    internal static class ContentExtractor
    {
        private static readonly IContentExtractor[] Extractors;

        static ContentExtractor()
        {
            var type = typeof(IContentExtractor);
            Extractors = Assembly.GetExecutingAssembly().DefinedTypes
                .Where(c => type.IsAssignableFrom(c.AsType()) && c.GetConstructor(Type.EmptyTypes) != null)
                .Select(Activator.CreateInstance)
                .Cast<IContentExtractor>()
                .ToArray();
        }

        public static string GetContent(Document doc, CancellationToken token)
        {
            var extractor = Extractors.FirstOrDefault(c => c.SupporterTypes.Contains(doc.Type));

            if(extractor != null)
            {
                try
                {
                    return extractor.GetContent(Path.Combine(doc.FullPath, doc.Name), token);
                }
                catch(Exception e)
                {
                    Logger.Instance.Warn("Не удалось извлечь контент файла '{0}': {1}", Path.Combine(doc.FullPath, doc.Name), (object)e);
                }
            }

            return String.Empty;
        }
    }
}