using Sentro.Utilities;
using System;
using System.IO;
using Sentro.Traffic;

namespace Sentro.Cache
{
    internal class CacheManager
    {
        public const string Tag = "CacheManager";               

        private static CacheManager _cacheManger;
        private static FileHierarchy _fileHierarchy;

        private CacheManager()
        {
            _fileHierarchy = FileHierarchy.GetInstance();                                   
        }

        public static CacheManager GetInstance()
        {           
            return _cacheManger ?? (_cacheManger = new CacheManager());
        }

        public void Cache(SentroRequest request, SentroResponse response)
        {
            var hash = request.RequestUriHashed();
            _fileHierarchy.WriteToTemp(hash,response.ToBytes());        
            return;

            bool isTemp = true;
            if (isTemp)
            {
                //MoveToHierarchy(hashedUrl);
            }
            else
            {
                //WriteToFileHierarchy(response, hashedUrl);
            }
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
            var hash = request.RequestUriHashed();
            if (_fileHierarchy.ExistInTemp(hash))
            {
                var bytes = _fileHierarchy.ReadFromTemp(hash);
                return SentroResponse.CreateFromBytes(bytes, bytes.Length);
            }

            return null;//TODO:remove this line
            string normalizedUrl = new Normalizer().Normalize(request.RequestUri());
            string hashedUrl = new Murmur2().Hash(normalizedUrl.ToBytes());
            
            // Evaluate destination folder
            string lvl1 = hashedUrl[0].ToString();
            string lvl2 = hashedUrl.Substring(1, 2);
            //string _destenationFile = String.Format("{0}\\{1}\\{2}\\{3}", _mainDirectory, lvl1, lvl2, hashedUrl);


            if (IsInCache(request))
            {
                return null;
            }
            else
            {
                // return HttpResponse From Cache
                //var bytes = File.ReadAllBytes(_destenationFile);
                //return new SentroResponse(bytes,bytes.Length);
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
