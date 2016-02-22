using System.IO;


namespace Sentro.Utilities
{
    class FileHierarchy
    {
        private readonly string _mainDirectory;
        private readonly string _tempDirectory;
        private readonly string _logsDirectory;
        private static FileHierarchy _fileHierarchy;

        private FileHierarchy()
        {
            _mainDirectory = Settings.GetInstance().Setting.Cache.Path;
            _tempDirectory = _mainDirectory + "/temp";
            _logsDirectory = _mainDirectory + "/logs";
            Init();
        }
        public static FileHierarchy GetInstance()
        {
            return _fileHierarchy ?? (_fileHierarchy = new FileHierarchy());
        }

        private void Init()
        {
            for (int i = 0; i < 16; i++)
            {                                            
                for (int k = 0; k < 256; k++)
                {                    
                    var folder = $"{_mainDirectory}/{i.ToString("X")}/{k.ToString("X2")}";
                    Directory.CreateDirectory(folder);
                }                
            }

            Directory.CreateDirectory(_tempDirectory);
            Directory.CreateDirectory(_logsDirectory);
        }


        public string TempDirectory => _tempDirectory;
        public string LogsDirectory => _logsDirectory;
        public string MainDirectory => _mainDirectory;

        public bool Exist(string hash)
        {
            return File.Exists(MapToFilePath(hash));
        }

        public bool ExistInTemp(string hash)
        {
            return File.Exists(MapToTempPath(hash));
        }

        public byte[] ReadFromTemp(string hash)
        {
            return File.ReadAllBytes(MapToTempPath(hash));
        }

        public byte[] Read(string hash)
        {
            return File.ReadAllBytes(MapToFilePath(hash));
        }

        public void WriteToTemp(string hash,byte[] bytes)
        {
            File.WriteAllBytes(MapToTempPath(hash),bytes);                  
        }

        public void Write(string hash, byte[] bytes)
        {
            File.WriteAllBytes(MapToFilePath(hash), bytes);
        }

        public void MoveFromTemp(string hash)
        {
            File.Move(MapToTempPath(hash), MapToFilePath(hash));
        }

        private string MapToTempPath(string hash)
        {
            return $"{_tempDirectory}/{hash}";
        }

        private string MapToFilePath(string hash)
        {
            return $"{_mainDirectory}/{hash[0]}/{hash.Substring(1, 2)}/{hash.Substring(3)}/{hash}";
        }
    }
}
