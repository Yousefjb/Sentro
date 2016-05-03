using System;
using System.IO;
using Sentro.Utilities;
using Sentro.Traffic;

namespace Sentro.Cache
{
    /*
        Responsibility : hold the logic of caching process
    */
    internal static class CacheManager
    {
        private const string Tag = "CacheManager";
        
        private static readonly FileHierarchy FileHierarchy;
        private static readonly FileLogger FileLogger;

        static CacheManager()
        {
            FileHierarchy = FileHierarchy.GetInstance();
            FileLogger = FileLogger.GetInstance();
        }

        public static bool IsCacheable(HttpResponseHeaders headers)
        {
            if (headers.StatusCode != 200)
                return false;
            //if (headers.ContentType.Contains("javascript"))
            //{
            //    FileLogger.Debug(Tag,"no javascript caching please");
            //    return false;
            //}                                

            return true;
        }

        public static FileStream OpenFileWriteStream(string hash)
        {
            var path = FileHierarchy.MapToFilePath(hash);
            return File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        }

        public static void Delete(string hash)
        {
            File.Delete(FileHierarchy.MapToFilePath(hash));
        }

        public static CacheResponse Get(string hash)
        {
            CacheResponse cacheResponse = null;

            try
            {
                if (FileHierarchy.Exist(hash))
                {
                    var path = FileHierarchy.MapToFilePath(hash);
                    FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (fs.Length == 0)
                    {
                        Delete(hash);
                        fs.Dispose();
                    }
                    else
                        cacheResponse = new CacheResponse(fs);
                }                
            }
            catch (Exception e)
            {
                FileLogger.Error(Tag, e.ToString());
            }

            return cacheResponse;
        }

        public static bool IsCached(string uriHash)
        {
            return FileHierarchy.Exist(uriHash);
        }
    }
}
