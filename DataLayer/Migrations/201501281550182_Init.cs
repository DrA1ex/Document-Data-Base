using System.Data.Entity.Migrations;

namespace DataLayer.Migrations
{
    public partial class Init : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BaseFolders",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        FullPath = c.String(nullable: false, maxLength: 4000),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.FullPath);
            
            CreateTable(
                "dbo.Folders",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        FullPath = c.String(nullable: false, maxLength: 4000),
                        Label = c.String(nullable: false, maxLength: 4000),
                        ParentId = c.Long(),
                        BaseFolderId = c.Long(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Folders", t => t.ParentId)
                .ForeignKey("dbo.BaseFolders", t => t.BaseFolderId)
                .Index(t => t.ParentId)
                .Index(t => t.BaseFolderId);
            
            CreateTable(
                "dbo.Documents",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 4000),
                        Type = c.Int(nullable: false),
                        ParentFolderId = c.Long(nullable: false),
                        LastEditDateTime = c.DateTime(nullable: false),
                        Cached = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Folders", t => t.ParentFolderId, cascadeDelete: true)
                .Index(t => t.ParentFolderId)
                .Index(t => t.LastEditDateTime)
                .Index(t => t.Cached);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Folders", "BaseFolderId", "dbo.BaseFolders");
            DropForeignKey("dbo.Folders", "ParentId", "dbo.Folders");
            DropForeignKey("dbo.Documents", "ParentFolderId", "dbo.Folders");
            DropIndex("dbo.Documents", new[] { "Cached" });
            DropIndex("dbo.Documents", new[] { "LastEditDateTime" });
            DropIndex("dbo.Documents", new[] { "ParentFolderId" });
            DropIndex("dbo.Folders", new[] { "BaseFolderId" });
            DropIndex("dbo.Folders", new[] { "ParentId" });
            DropIndex("dbo.BaseFolders", new[] { "FullPath" });
            DropTable("dbo.Documents");
            DropTable("dbo.Folders");
            DropTable("dbo.BaseFolders");
        }
    }
}
