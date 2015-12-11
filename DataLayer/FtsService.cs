using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Windows.Media;
using Common.Utils;
using Lucene.Net.Analysis.Ru;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Store;
using DocumentData = DataLayer.Model.Document;
using Version = Lucene.Net.Util.Version;

namespace DataLayer
{
    public static class FtsService
    {
        public static string[] HighlightTags { get; set; }
        public static readonly string LuceneDir = Path.Combine(Environment.CurrentDirectory, "data", "lucene_index");
        private static FSDirectory _directoryTemp;
        private static readonly RussianAnalyzer Analyzer = new RussianAnalyzer(Version.LUCENE_30);
        private static Color _highlightingColor;

        public static Color HighlightingColor
        {
            get { return _highlightingColor; }
            set
            {
                _highlightingColor = value;
                HighlightTags =
                    new[]
                    {
                        String.Format("[color={0}][b]", value),
                        "[/b][/color]"
                    };
            }
        }

        static FtsService()
        {
            HighlightingColor = Colors.Red;
        }

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
                    try
                    {
                        File.Delete(lockFilePath);
                    }
                    catch(Exception e)
                    {
                        Logger.Instance.Warn("Unable to delete lucene write.lock {0}", (object)e);
                    }
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
            doc.Add(new Field("Name", data.Name, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Content", documentContent, Field.Store.YES, Field.Index.ANALYZED));

            writer.AddDocument(doc);
        }

        public static void AddUpdateLuceneIndex(DocumentData documentData, string documentContent)
        {
            using(var writer = new IndexWriter(Directory, Analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                AddToLuceneIndex(documentData, documentContent, writer);
            }
        }

        public static void ClearLuceneIndexRecord(long docId)
        {
            using(var writer = new IndexWriter(Directory, Analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                var searchQuery = new TermQuery(new Term("Id", docId.ToString()));
                writer.DeleteDocuments(searchQuery);
            }
        }

        public static bool ClearLuceneIndex()
        {
            try
            {
                using(var writer = new IndexWriter(Directory, Analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    writer.DeleteAll();
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
            using(var writer = new IndexWriter(Directory, Analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                writer.Optimize();
            }
        }

        private static DocumentData MapLuceneDocumentToData(Document doc, Highlighter highlighter)
        {
            var id = Convert.ToInt64(doc.Get("Id"));
            var name = doc.Get("Name");
            var content = doc.Get("Content");

            return new DocumentData
                   {
                       Id = id,
                       Name = highlighter.GetBestFragment(Analyzer, "Name", name) ?? name,
                       DocumentContent = highlighter.GetBestFragment(Analyzer, "Content", content) ?? (String.Join(" ", content.Split(' ').Take(10)) + "...")
                   };
        }

        private static IEnumerable<DocumentData> MapLuceneToDataList(IEnumerable<Document> hits, Highlighter highlighter)
        {
            return hits.Select(c => MapLuceneDocumentToData(c, highlighter)).ToList();
        }

        private static IEnumerable<DocumentData> MapLuceneToDataList(IEnumerable<ScoreDoc> hits, IndexSearcher searcher, Highlighter highlighter)
        {
            return hits.Select(hit => MapLuceneDocumentToData(searcher.Doc(hit.Doc), highlighter)).ToList();
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
                var searchFieldIsEmpty = string.IsNullOrEmpty(searchField);
                QueryParser parser;
                if(!searchFieldIsEmpty)
                {
                    parser = new QueryParser(Version.LUCENE_30, searchField, Analyzer);
                }
                else
                {
                    parser = new MultiFieldQueryParser
                        (Version.LUCENE_30, new[] { "Id", "Name", "Content" }, Analyzer);
                }

                var query = ParseQuery(searchQuery, parser);
                var scorer = new QueryScorer(query);
                var formatter = new SimpleHTMLFormatter(HighlightTags[0], HighlightTags[1]);
                var highlighter = new Highlighter(formatter, scorer)
                                  {
                                      TextFragmenter = new SimpleSpanFragmenter(scorer, 500),
                                      MaxDocCharsToAnalyze = int.MaxValue
                                  };

                if(!searchFieldIsEmpty)
                {
                    var hits = searcher.Search(query, hitsLimit).ScoreDocs;
                    var results = MapLuceneToDataList(hits, searcher, highlighter);
                    return results;
                }
                else
                {
                    var hits = searcher.Search
                        (query, null, hitsLimit, Sort.RELEVANCE).ScoreDocs;
                    var results = MapLuceneToDataList(hits, searcher, highlighter);
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

            input = input.Trim().Replace("-", " ");

            GC.Collect();

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

            var docs = new List<Document>();

            using(var searcher = new IndexSearcher(Directory, false))
            using(var reader = IndexReader.Open(Directory, false))
            {
                var term = reader.TermDocs();
                while(term.Next())
                {
                    docs.Add(searcher.Doc(term.Doc));
                }
            }
            return MapLuceneToDataList(docs, null);
        }
    }
}