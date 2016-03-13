using System;
using System.IO;
using Sentro.Utilities;
using Sentro.Traffic;

namespace Sentro.Cache
{
    /*
        Responsibility : hold the logic of caching process
    */
    internal class CacheManager
    {
        public const string Tag = "CacheManager";

        private static CacheManager _cacheManger;
        private static FileHierarchy _fileHierarchy;
        private static FileLogger _fileLogger;

        private CacheManager()
        {
            _fileHierarchy = FileHierarchy.GetInstance();
            _fileLogger = FileLogger.GetInstance();
        }

        public static CacheManager GetInstance()
        {
            return _cacheManger ?? (_cacheManger = new CacheManager());
        }      

        public static Cacheable IsCacheable(SentroResponse response)
        {
            return Cacheable.Yes;
        }

        public static FileStream OpenFileWriteStream(string hash)
        {
            var path = _fileHierarchy.MapToFilePath(hash);
            return File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);                       
        }

        public static FileStream OpenFileReadStream(string hash)
        {
            var path = _fileHierarchy.MapToFilePath(hash);
            return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }       

        public CacheResponse Get(SentroRequest request)
        {            
            try
            {
                var hash = request.RequestUriHashed();
                if (!_fileHierarchy.Exist(hash)) return null;
                var path = _fileHierarchy.MapToFilePath(hash);
                FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var cacheResponse = new CacheResponse(fs);
                return cacheResponse;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());
                return null;
            }
        }

        public enum Cacheable
        {
            Yes,
            No,
            NotDetermined
        }
    }
}
