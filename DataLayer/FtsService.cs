using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using DocumentData = DataLayer.Model.Document;
using Version = Lucene.Net.Util.Version;

namespace DataLayer
{
    public static class FtsService
    {
        private static readonly string LuceneDir = Path.Combine(Environment.CurrentDirectory, "data", "lucene_index");
        private static FSDirectory _directoryTemp;

        private static FSDirectory Directory
        {
            get
            {
                if(_directoryTemp == null)
                {
                    _directoryTemp = FSDirectory.Open(new DirectoryInfo(LuceneDir));
                }
                if(IndexWriter.IsLocked(_directoryTemp))
                {
                    IndexWriter.Unlock(_directoryTemp);
                }
                var lockFilePath = Path.Combine(LuceneDir, "write.lock");
                if(File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }
                return _directoryTemp;
            }
        }

        private static void AddToLuceneIndex(DocumentData data, string documentContent, IndexWriter writer)
        {
            var searchQuery = new TermQuery(new Term("Id", data.Id.ToString()));
            writer.DeleteDocuments(searchQuery);

            var doc = new Document();

            doc.Add(new Field("Id", data.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("Name", Path.GetFileNameWithoutExtension(data.Name), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Content", documentContent, Field.Store.YES, Field.Index.ANALYZED));

            writer.AddDocument(doc);

            data.Cached = true;
        }

        public static void AddUpdateLuceneIndex(DocumentData documentData, string documentContent)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using(var writer = new IndexWriter(Directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                AddToLuceneIndex(documentData, documentContent, writer);

                analyzer.Close();
                writer.Dispose();
            }
        }

        public static void ClearLuceneIndexRecord(long docId)
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using(var writer = new IndexWriter(Directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                var searchQuery = new TermQuery(new Term("Id", docId.ToString()));
                writer.DeleteDocuments(searchQuery);

                analyzer.Close();
                writer.Dispose();
            }
        }

        public static bool ClearLuceneIndex()
        {
            try
            {
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);
                using(var writer = new IndexWriter(Directory, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    writer.DeleteAll();

                    analyzer.Close();
                    writer.Dispose();
                }
            }
            catch(Exception)
            {
                return false;
            }
            return true;
        }

        public static void Optimize()
        {
            var analyzer = new StandardAnalyzer(Version.LUCENE_30);
            using(var writer = new IndexWriter(Directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                analyzer.Close();
                writer.Optimize();
                writer.Dispose();
            }
        }

        private static DocumentData MapLuceneDocumentToData(Document doc)
        {
            return new DocumentData
                   {
                       Id = Convert.ToInt64(doc.Get("Id")),
                       Name = doc.Get("Name"),
                       FtsCaptures = doc.Get("Content")
                   };
        }

        private static IEnumerable<DocumentData> MapLuceneToDataList(IEnumerable<Document> hits)
        {
            return hits.Select(MapLuceneDocumentToData).ToList();
        }

        private static IEnumerable<DocumentData> MapLuceneToDataList(IEnumerable<ScoreDoc> hits,
            IndexSearcher searcher)
        {
            return hits.Select(hit => MapLuceneDocumentToData(searcher.Doc(hit.Doc))).ToList();
        }

        private static Query ParseQuery(string searchQuery, QueryParser parser)
        {
            Query query;
            try
            {
                query = parser.Parse(searchQuery.Trim());
            }
            catch(ParseException)
            {
                query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }
            return query;
        }

        private static IEnumerable<DocumentData> SearchInternal(string searchQuery, string searchField = "")
        {
            if(string.IsNullOrEmpty(searchQuery.Replace("*", "").Replace("?", "")))
            {
                return new List<DocumentData>();
            }

            using(var searcher = new IndexSearcher(Directory, false))
            {
                const int hitsLimit = 1000;
                var analyzer = new StandardAnalyzer(Version.LUCENE_30);

                if(!string.IsNullOrEmpty(searchField))
                {
                    var parser = new QueryParser(Version.LUCENE_30, searchField, analyzer);
                    var query = ParseQuery(searchQuery, parser);
                    var hits = searcher.Search(query, hitsLimit).ScoreDocs;
                    var results = MapLuceneToDataList(hits, searcher);
                    analyzer.Close();
                    searcher.Dispose();
                    return results;
                }
                else
                {
                    var parser = new MultiFieldQueryParser
                        (Version.LUCENE_30, new[] { "Id", "Name", "Content" }, analyzer);
                    var query = ParseQuery(searchQuery, parser);
                    var hits = searcher.Search
                        (query, null, hitsLimit, Sort.RELEVANCE).ScoreDocs;
                    var results = MapLuceneToDataList(hits, searcher);
                    analyzer.Close();
                    searcher.Dispose();
                    return results;
                }
            }
        }

        public static IEnumerable<DocumentData> Search(string input, string fieldName = "")
        {
            if(string.IsNullOrEmpty(input))
            {
                return new List<DocumentData>();
            }

            var terms = input.Trim().Replace("-", " ").Split(' ')
                .Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim() + "*");
            input = string.Join(" ", terms);

            return SearchInternal(input, fieldName);
        }

        public static IEnumerable<DocumentData> SearchDefault(string input, string fieldName = "")
        {
            return string.IsNullOrEmpty(input) ? new List<DocumentData>() : SearchInternal(input, fieldName);
        }

        public static IEnumerable<DocumentData> GetAllIndexRecords()
        {
            if(!System.IO.Directory.EnumerateFiles(LuceneDir).Any())
            {
                return new List<DocumentData>();
            }

            var searcher = new IndexSearcher(Directory, false);
            var reader = IndexReader.Open(Directory, false);
            var docs = new List<Document>();
            var term = reader.TermDocs();
            while(term.Next())
            {
                docs.Add(searcher.Doc(term.Doc));
            }
            reader.Dispose();
            searcher.Dispose();
            return MapLuceneToDataList(docs);
        }
    }
}