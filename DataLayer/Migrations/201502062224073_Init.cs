namespace DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Init : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Documents",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 4000),
                        Type = c.Int(nullable: false),
                        FullPath = c.String(nullable: false, maxLength: 4000),
                        LastEditDateTime = c.DateTime(nullable: false),
                        Cached = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name)
                .Index(t => t.FullPath)
                .Index(t => t.LastEditDateTime)
                .Index(t => t.Cached);
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.Documents", new[] { "Cached" });
            DropIndex("dbo.Documents", new[] { "LastEditDateTime" });
            DropIndex("dbo.Documents", new[] { "FullPath" });
            DropIndex("dbo.Documents", new[] { "Name" });
            DropTable("dbo.Documents");
        }
    }
}
