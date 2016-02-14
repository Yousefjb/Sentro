using System;

namespace Sentro
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("------------------------\n|    Cache Manager    |\n------------------------\n");
            Sentro.CacheManager.CacheManager obj = new CacheManager.CacheManager();
            string given_url = "";
            obj.Hier();
            do
            {
                Console.Write("\nInput: ");
                given_url = Console.ReadLine();
                //string _normalized = obj.normalize(given_url);
                //string _hashed = obj.hash(_normalized);
                

            } while (given_url!="*");
            
            
        }
    }
}
