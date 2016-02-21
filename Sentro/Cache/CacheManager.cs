using Sentro.Utilities;
using System;
using System.IO;
using Sentro.Traffic;

namespace Sentro.Cache
{
    internal class CacheManager
    {
        public const string Tag = "CacheManager";        
        private string _mainDirectory = "C:/Sentro/CacheStorage";

        private static CacheManager _cacheManger;

        private CacheManager()
        {
            var hirarachy = FileHierarchy.GetInstance();
                        
            //string url = "www.google.com";
            //string hashedUrl = new Murmur2().Hash(url.ToBytes());
        }

        public static CacheManager GetInstance()
        {           
            return _cacheManger ?? (_cacheManger = new CacheManager());
        }

        public void Cache(SentroRequest request, SentroResponse response)
        {
            string normalizedUrl = new Normalizer().Normalize(request.RequestUri());
            string hashedUrl = new Murmur2().Hash(normalizedUrl.ToBytes());


            bool isTemp = true;
            if (isTemp)
            {
                MoveToHierarchy(hashedUrl);
            }
            else
            {
                WriteToFileHierarchy(response, hashedUrl);
            }
        }
        void MoveToHierarchy(string hashedUrl)
        {
            string _tmpDirectory = _mainDirectory + "/tmp";
            string _tmpFile = _tmpDirectory + hashedUrl;

            // Evaluate destination folder
            string lvl1 = hashedUrl[0].ToString();
            string lvl2 = hashedUrl.Substring(1, 2);
            string _destenationFile = String.Format("{0}\\{1}\\{2}\\{3}", _mainDirectory, lvl1, lvl2, hashedUrl);

            // Start moving
            if (Directory.Exists(_tmpFile))
            {
                File.Move(_tmpFile, _destenationFile);
            }
            else
            {
                //ERROR
                //LOG: The temp directory foes not exist
            }
        }
        void WriteToFileHierarchy(SentroResponse response, string hashedUrl)
        {
            // Evaluate destination folder
            string lvl1 = hashedUrl[0].ToString();
            string lvl2 = hashedUrl.Substring(1, 2);
            string _destenationFile = String.Format("{0}\\{1}\\{2}\\{3}", _mainDirectory, lvl1, lvl2, hashedUrl);

            File.WriteAllBytes(_destenationFile,response.ToBytes());
            

        }
        bool isCachable(SentroResponse response)
        {
            //
            return true;
        }
        bool isFullStorage()
        {
            return true;
        }
        public SentroResponse Get(SentroRequest request)
        {
            return null;//TODO:remove this line
            string normalizedUrl = new Normalizer().Normalize(request.RequestUri());
            string hashedUrl = new Murmur2().Hash(normalizedUrl.ToBytes());
            
            // Evaluate destination folder
            string lvl1 = hashedUrl[0].ToString();
            string lvl2 = hashedUrl.Substring(1, 2);
            string _destenationFile = String.Format("{0}\\{1}\\{2}\\{3}", _mainDirectory, lvl1, lvl2, hashedUrl);


            if (IsInCache(request))
            {
                return null;
            }
            else
            {
                // return HttpResponse From Cache
                var bytes = File.ReadAllBytes(_destenationFile);
                return new SentroResponse(bytes,bytes.Length);
            }

           
        }
        bool IsInCache(SentroRequest req)
        {
            return true;
        }
        /*
        
        public void Cache(HttpRequest request, HttpResponse response)
        {
        response.MaxCacheAge
           
        response -> bool InTemp
           
        if(InTemp) 
          moveTohir
          
        else
          WriteToFileHirar(response.ToBytes())                                      

           if(response is cacheable)
           cache it

            if(nospace)
             free some space

        }

        public HttpResponse Get(HttpRequest request)
        {
           if(isNotInCache(request))
             return null;

            if(exist)
             return HttpResponse From Cache
        }
            
         */

    }
}
