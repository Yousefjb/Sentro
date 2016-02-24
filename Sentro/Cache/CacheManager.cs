using System;
using Sentro.Utilities;
using Sentro.Traffic;

namespace Sentro.Cache
{
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
                var hash = request.RequestUriHashed();
                _fileHierarchy.WriteToTemp(hash, response.ToBytes());
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());
            }
        }

        public SentroResponse Get(SentroRequest request)
        {
            try
            {
                var hash = request.RequestUriHashed();
                SentroResponse response = null;
                if (_fileHierarchy.ExistInTemp(hash))
                {
                    var bytes = _fileHierarchy.ReadFromTemp(hash);
                    response =  SentroResponse.CreateFromBytes(bytes, bytes.Length);
                }
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
