using System.Data.Entity.Migrations;

namespace DataLayer.Migrations
{
    public partial class StoreOnlyDocuments : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Documents", "ParentFolderId", "dbo.Folders");
            DropForeignKey("dbo.Folders", "ParentId", "dbo.Folders");
            DropForeignKey("dbo.Folders", "BaseFolderId", "dbo.BaseFolders");
            DropIndex("dbo.BaseFolders", new[] { "FullPath" });
            DropIndex("dbo.Folders", new[] { "ParentId" });
            DropIndex("dbo.Folders", new[] { "BaseFolderId" });
            DropIndex("dbo.Documents", new[] { "ParentFolderId" });
            AddColumn("dbo.Documents", "FullPath", c => c.String(maxLength: 4000));
            DropColumn("dbo.Documents", "ParentFolderId");
            DropTable("dbo.BaseFolders");
            DropTable("dbo.Folders");
        }
        
        public override void Down()
        {
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
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.BaseFolders",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        FullPath = c.String(nullable: false, maxLength: 4000),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.Documents", "ParentFolderId", c => c.Long(nullable: false));
            DropColumn("dbo.Documents", "FullPath");
            CreateIndex("dbo.Documents", "ParentFolderId");
            CreateIndex("dbo.Folders", "BaseFolderId");
            CreateIndex("dbo.Folders", "ParentId");
            CreateIndex("dbo.BaseFolders", "FullPath");
            AddForeignKey("dbo.Folders", "BaseFolderId", "dbo.BaseFolders", "Id");
            AddForeignKey("dbo.Folders", "ParentId", "dbo.Folders", "Id");
            AddForeignKey("dbo.Documents", "ParentFolderId", "dbo.Folders", "Id", cascadeDelete: true);
        }
    }
}
