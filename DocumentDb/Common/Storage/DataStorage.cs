using System;
using System.IO;
using DocumentDb.Common.Utils;
using Newtonsoft.Json;

namespace DocumentDb.Common.Storage
{
    public class DataStorage<TStorage> : IDisposable
        where TStorage : class
    {
        private static readonly string StorageName = String.Format("storage.{0}.json", typeof(TStorage).Name);

        public static readonly DataStorage<TStorage> Instance = new DataStorage<TStorage>();
        private readonly object _syncDummy = new object();
        private bool _disposed;

        private DataStorage()
        {
            Load(StorageName);
        }

        private bool HasChanges { get; set; }

        private TStorage Storage { get; set; }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                GC.SuppressFinalize(this);

                if (HasChanges)
                {
                    Save(StorageName);
                }
            }
        }

        ~DataStorage()
        {
            Dispose();
        }

        public TStorage GetStorage()
        {
            lock (_syncDummy)
            {
                return Storage;
            }
        }

        public void UpdateStorage(TStorage storage)
        {
            lock (_syncDummy)
            {
                Storage = storage;
            }
            HasChanges = true;
        }

        public void Refresh()
        {
            Load(StorageName);
        }

        public void Save()
        {
            Save(StorageName);
        }

        private void Load(string fileName)
        {
            lock (_syncDummy)
            {
                HasChanges = false;

                if (File.Exists(fileName))
                {
                    try
                    {
                        var fileText = File.ReadAllText(fileName);
                        Storage = JsonConvert.DeserializeObject<TStorage>(fileText);
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Fatal("Unable to load storage", e);
                    }
                }
            }
        }

        private void Save(string fileName)
        {
            if (!HasChanges)
                return;

            lock (_syncDummy)
            {
                string newFileName = fileName + ".new";

                try
                {
                    var serializedObject = JsonConvert.SerializeObject(Storage, Formatting.Indented);
                    File.WriteAllText(newFileName, serializedObject);

                    if (File.Exists(fileName))
                        File.Replace(newFileName, fileName, fileName + ".old");
                    else
                    {
                        File.Move(newFileName, fileName);
                    }

                    HasChanges = false;
                }
                catch (Exception e)
                {
                    Logger.Instance.Fatal("Unable to save storage", e);
                }
            }
        }
    }
}