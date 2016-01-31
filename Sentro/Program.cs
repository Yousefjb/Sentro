using System;

namespace Sentro
{
    class Program
    {
        static void Main(string[] args)
        {
            string given_url = Console.ReadLine();
            Sentro.CacheManager.CacheManager obj = new CacheManager.CacheManager();
            obj.normalize(given_url);
            
        }
    }
}
