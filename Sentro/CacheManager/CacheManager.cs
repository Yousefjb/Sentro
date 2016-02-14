
namespace Sentro.CacheManager
{
    internal class CacheManager
    {
        public static string Tag = "CacheManager";

        private CacheManager _cacheManger;

        private CacheManager()
        {
            var hirarachy = FileHierarchy.GetInstance();
                        
            //string url = "www.google.com";
            //string hashedUrl = new Murmur2().Hash(url.ToBytes());
        }

        public CacheManager GetInstance()
        {           
            return _cacheManger ?? (_cacheManger = new CacheManager());
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
           if(ifNotInCache(request))
             return null;

            if(exist)
             return HttpResponse From Cache
        }
            
         */                      

    }
}
