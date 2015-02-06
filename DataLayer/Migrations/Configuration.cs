using System.Data.Entity.Migrations;

namespace DataLayer.Migrations
{
    public sealed class Configuration : DbMigrationsConfiguration<DdbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(DdbContext context)
        {
        }
    }
}