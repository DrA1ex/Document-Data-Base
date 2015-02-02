using System.Data.Entity;
using DataLayer.Model;

namespace DataLayer
{
    public class DdbContext : DbContext
    {
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<BaseFolder> BaseFolders { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Folder>()
                .HasMany<Folder>(c => c.Folders)
                .WithOptional(c => c.Parent)
                .HasForeignKey(c => c.ParentId);

            modelBuilder.Entity<BaseFolder>()
                .HasMany(c => c.Folders)
                .WithOptional(c => c.BaseFolder)
                .HasForeignKey(c => c.BaseFolderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Folder>()
                .HasMany(c => c.Documents)
                .WithRequired(c => c.ParentFolder)
                .HasForeignKey(c => c.ParentFolderId)
                .WillCascadeOnDelete();
        }
    }
}