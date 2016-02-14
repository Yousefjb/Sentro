using System;
using System.IO;


namespace Sentro.CacheManager
{
    class FileHierarchy
    {
        private string _mainDirectory;

        public FileHierarchy _fileHierarchy;
        private FileHierarchy()
        {
            _mainDirectory = "C:/Sentro/CacheStorage";
            Init();
        }
        public FileHierarchy GetInstance()
        {
            if (_fileHierarchy == null)
                _fileHierarchy = new FileHierarchy();
            return _fileHierarchy;
        }
        private void Init()
        {
            // Create Main folder if not exist
            if (!Directory.Exists(_mainDirectory))
                Directory.CreateDirectory(_mainDirectory);
            string level1path, level2path;
            // Create first level folders
            for (int i = 0; i < 16; i++)
            {
                level1path = _mainDirectory + "/" + i.ToString("X");
                if (!Directory.Exists(level1path))
                    Directory.CreateDirectory(level1path);
                // Create second level folders
                for (int k = 0; k < 256; k++)
                {
                    level2path = level1path + "/" + k.ToString("X2");
                    if (!Directory.Exists(level2path))
                        Directory.CreateDirectory(level2path);
                }
                //Console.WriteLine(level1path + " ..Created");

            }
           
        }
    }
}
