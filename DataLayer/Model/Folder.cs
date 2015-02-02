using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace DataLayer.Model
{
    public class Folder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public string FullPath { get; set; }

        [Required]
        public string Label { get; set; }

        public long? ParentId { get; set; }
        public Folder Parent { get; set; }
        public long? BaseFolderId { get; set; }
        public BaseFolder BaseFolder { get; set; }
        public virtual ICollection<Folder> Folders { get; set; }
        public virtual ICollection<Document> Documents { get; set; }

        [NotMapped]
        public bool HasChildren { get { return (Documents != null && Documents.Any()) || (Folders != null && Folders.Any()); } }
    }
}