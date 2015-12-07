using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DataLayer.Model
{
    public class Document : ICloneable, INotifyPropertyChanged
    {
        private bool _cached;
        private DateTime _lastEditDateTime;
        private string _name;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Index]
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        [Required]
        public DocumentType Type { get; set; }

        [Required]
        [Index]
        public string FullPath { get; set; }

        [Index]
        public DateTime LastEditDateTime
        {
            get { return _lastEditDateTime; }
            set
            {
                _lastEditDateTime = value;
                OnPropertyChanged();
            }
        }

        [Index]
        public bool Cached
        {
            get { return _cached; }
            set
            {
                _cached = value;
                OnPropertyChanged();
            }
        }

        [NotMapped]
        public string DocumentContent { get; set; }

        [NotMapped]
        public long Order { get; set; }


        public object Clone()
        {
            return MemberwiseClone();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if(PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
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