using System;
using System.IO;
using System.Threading.Tasks;
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

        public static bool IsCacheable(HttpResponseHeaders headers)
        {
            bool result = true;           
            return result;
        }

        public static FileStream OpenFileWriteStream(string hash)
        {
            var path = _fileHierarchy.MapToFilePath(hash);
            return File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);                       
        }

        public static FileStream OpenFileReadStream(string hash)
        {
            var path = _fileHierarchy.MapToFilePath(hash);
            return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public static void Delete(int hash)
        {
            Delete(hash.ToString("X8"));
        }

        public static void Delete(string hash)
        {
            File.Delete(_fileHierarchy.MapToFilePath(hash));
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

        public static CacheResponse Get(string hash)
        {
            CacheResponse cacheResponse = null;
            try
            {
                if (!_fileHierarchy.Exist(hash)) return null;
                var path = _fileHierarchy.MapToFilePath(hash);
                FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                cacheResponse = new CacheResponse(fs);
                return cacheResponse;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag, e.ToString());
            }

            return cacheResponse;
        }

        public static bool ShouldValidiate(string uriHash)
        {
            return false;
        }

        public static bool IsCached(string uriHash)
        {
            return _fileHierarchy.Exist(uriHash);
        }

        public enum Cacheable
        {
            Yes,
            No,
            NotDetermined
        }
    }
}
