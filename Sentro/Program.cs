using System;

namespace Sentro
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("------------------------\n|    Url Normalizer    |\n------------------------\n");
            Sentro.CacheManager.CacheManager obj = new CacheManager.CacheManager();
            string given_url;
            do
            {
                Console.Write("input: ");
                given_url = Console.ReadLine();
                obj.normalize(given_url);

            } while (given_url!="*");
            
            
        }
    }
}
