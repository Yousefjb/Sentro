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

        public async static Task<bool> IsCacheable(HttpResponseHeaders headers)
        {
            bool result = false;
            await Task.Run(() =>
            {
                result = true;                
            });
            return result;
        }

        public static async Task<FileStream> OpenFileWriteStream(string hash)
        {
            FileStream result = null;
            await Task.Run(() =>
            {
                var path = FileHierarchy.MapToFilePath(hash);
                result = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            });
            return result;
        }

        public static async Task Delete(int hash)
        {
            await Task.Run(() =>
            {
                File.Delete(FileHierarchy.MapToFilePath(hash.ToString("X8")));
            });
        }

        public static async Task<CacheResponse> Get(string hash)
        {
            CacheResponse cacheResponse = null;
            await Task.Run(() =>
            {
                try
                {
                    if (!FileHierarchy.Exist(hash)) return;
                    var path = FileHierarchy.MapToFilePath(hash);
                    FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    cacheResponse = new CacheResponse(fs);
                }
                catch (Exception e)
                {
                    FileLogger.Error(Tag, e.ToString());
                }
            });

            return cacheResponse;
        }

        public static async Task<bool> IsCached(string uriHash)
        {
            bool result = false;
            await Task.Run(() =>
            {
                result = FileHierarchy.Exist(uriHash);
            });
            return result;
        }       
    }
}
