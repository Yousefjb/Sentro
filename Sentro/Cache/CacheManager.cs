using System;
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

        public void Cache(SentroRequest request, SentroResponse response)
        {            
            try
            {
                _fileLogger.Debug(Tag,"caching this url " + request.RequestUri());
                var hash = request.RequestUriHashed();                
                Writer.WriteAsync(response.ToBytes(),_fileHierarchy.MapToFilePath(hash));              
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());
            }
        }

        public static bool IsCacheable(SentroResponse response)
        {
            return true;
        }

        public SentroResponse Get(SentroRequest request)
        {            
            try
            {
                var hash = request.RequestUriHashed();
                if (!_fileHierarchy.Exist(hash)) return null;           
                var bytes = Reader.ReadBytes(_fileHierarchy.MapToFilePath(hash));
                var response = SentroResponse.CreateFromBytes(bytes, bytes.Length);
                return response;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());
                return null;
            }
        }
    }
}
