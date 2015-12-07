using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;

namespace DataLayer.Model
{
    public class Document : ICloneable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Index]
        public string Name { get; set; }

        [Required]
        public DocumentType Type { get; set; }

        [Required]
        [Index]
        public string FullPath { get; set; }

        [Index]
        public DateTime LastEditDateTime { get; set; }

        [Index]
        public bool Cached { get; set; }

        [NotMapped]
        public string DocumentContent { get; set; }

        [NotMapped]
        public long Order { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    public static class DocumentUtils
    {
        public static Document FindFile(this DbSet<Document> documents, string fullPath, string name)
        {
            return documents.SingleOrDefault(c => c.FullPath == fullPath && c.Name == name)
                ?? documents.Local.FindFile(fullPath, name);
        }

        public static Document FindFile(this IEnumerable<Document> documents, string fullPath, string name)
        {
            return documents.SingleOrDefault(c => c.FullPath == fullPath && c.Name == name);
        }

        public static bool HasFile(this IEnumerable<Document> documents, string fullPath, string name)
        {
            return documents.Any(c => c.FullPath == fullPath && c.Name == name);
        }
    }
}