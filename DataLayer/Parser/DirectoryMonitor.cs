﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Common.Utils;
using DataLayer.Model;
using ExtensionAttribute = DataLayer.Attributes.ExtensionAttribute;

namespace DataLayer.Parser
{
    public enum DocumentMonitorState
    {
        Idle,
        Running
    }

    public class DirectoryMonitor : INotifyPropertyChanged
    {
        private static readonly dynamic[] Types;
        private DocumentMonitorState _state;

        public DirectoryMonitor(string path)
        {
            AbsolutePath = Path.GetFullPath(path);
            SynchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        public DocumentMonitorState State
        {
            get { return _state; }
            set
            {
                _state = value;
                OnPropertyChanged();
            }
        }

        private SynchronizationContext SynchronizationContext { get; set; }

        static DirectoryMonitor()
        {
            // ReSharper disable once CoVariantArrayConversion
            Types = ((DocumentType[])Enum.GetValues(typeof(DocumentType)))
                .Select(c => new { Attribute = c.GetAttributeOfType<ExtensionAttribute>(), Value = c })
                .Where(c => c.Attribute != null)
                .Select(c => new { c.Attribute.Extension, c.Value })
                .ToArray();
        }

        public string AbsolutePath { get; private set; }

        public void Update()
        {
            if(State == DocumentMonitorState.Running)
            {
                return;
            }

            SynchronizationContext.Send(c => State = DocumentMonitorState.Running, null);


            Task.Run(() =>
                     {
                         try
                         {
                             using(var ctx = new DdbContext())
                             {
                                 var baseDir = ctx.BaseFolders.SingleOrDefault(c => c.FullPath == AbsolutePath);
                                 if(baseDir == null)
                                 {
                                     baseDir = new BaseFolder { FullPath = AbsolutePath };
                                     ctx.BaseFolders.Add(baseDir);
                                     ctx.SaveChanges();
                                 }

                                 var directories = Directory.GetDirectories(AbsolutePath);
                                 foreach(var directory in directories)
                                 {
                                     ProcessDirectory(ctx, baseDir, Path.GetFullPath(directory));
                                 }

                                 ctx.SaveChanges();
                             }
                         }
                         catch(Exception e)
                         {
                             Logger.Instance.ErrorException("Unable to update directory cache: {0}", e);
                         }
                         finally
                         {
                             SynchronizationContext.Send(c => State = DocumentMonitorState.Idle, null);
                         }
                     });
        }

        private void ProcessDirectory(DdbContext ctx, BaseFolder baseFolder, string path, Folder parentFolder = null)
        {
            if(!Directory.Exists(path))
            {
                return;
            }

            Folder folder;
            if(parentFolder != null && parentFolder.Folders != null)
            {
                folder = parentFolder.Folders.SingleOrDefault(c => c.FullPath == path);
            }
            else if(baseFolder.Folders != null)
            {
                folder = baseFolder.Folders.SingleOrDefault(c => c.FullPath == path);
            }
            else
            {
                folder = ctx.Folders.SingleOrDefault(c => c.FullPath == path);
            }

            if(folder == null)
            {
                folder = new Folder
                         {
                             FullPath = path,
                             Label = Path.GetFileName(path)
                         };

                if(parentFolder == null)
                {
                    folder.BaseFolder = baseFolder;
                    ctx.Folders.Add(folder);
                }
                else
                {
                    folder.Parent = parentFolder;
                    ctx.Folders.Add(folder);
                }
            }


            ProcessFilesInFolder(ctx, folder);

            string[] directories = { };

            try
            {
                directories = Directory.GetDirectories(path);
            }
            catch(DirectoryNotFoundException e)
            {
                Logger.Instance.Warn("Не удалось получить субдиректории каталога '{0}': {1}", path, e);
            }

            foreach(var directory in directories)
            {
                ProcessDirectory(ctx, baseFolder, Path.GetFullPath(directory), folder);
            }
        }

        private void ProcessFilesInFolder(DdbContext ctx, Folder folder)
        {
            string[] filesInDirectory = { };
            try
            {
                filesInDirectory = Directory.GetFiles(folder.FullPath);
            }
            catch(DirectoryNotFoundException e)
            {
                Logger.Instance.Warn("Не удалось получить файлы каталога '{0}': {1}", folder.FullPath, e);
            }

            foreach(var file in filesInDirectory)
            {
                ProcessFileInternal(ctx, folder, file);
            }
        }

        private void ProcessFileInternal(DdbContext ctx, Folder folder, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileType = GetTypeForFileName(fileName);

            if(fileType == DocumentType.Undefined)
            {
                return;
            }

            Document document = null;
            if(folder.Folders != null)
            {
                document = folder.Documents.SingleOrDefault(c => c.Name == Path.GetFileName(fileName));
            }

            var lastEditTime = File.GetLastWriteTime(filePath);
            if(document == null)
            {
                document = new Document
                           {
                               Name = fileName,
                               Type = fileType,
                               ParentFolder = folder,
                               Cached = false,
                               LastEditDateTime = lastEditTime
                           };

                ctx.Documents.Add(document);
            }
            else if(lastEditTime != document.LastEditDateTime)
            {
                document.LastEditDateTime = lastEditTime;
                document.Cached = false;
            }
        }

        private DocumentType GetTypeForFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName.ToLower());

            if(extension.Length > 0)
            {
                extension = extension.Substring(1);

                var value = Types.SingleOrDefault(c => c.Extension == extension);
                if(value != null)
                {
                    return value.Value;
                }
            }

            return DocumentType.Undefined;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if(handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}