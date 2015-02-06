﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Model
{
    public class Document
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
    }
}