using System.Data.Entity;
using DataLayer.Model;

namespace DataLayer
{
    public class DdbContext : DbContext
    {
        public DbSet<Document> Documents { get; set; }
    }
}