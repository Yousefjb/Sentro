using System.IO;

namespace Sentro.Utilities
{
    /*
        Responsibility : nothing
        TODO : delete
    */
    static class Reader
    {
        public static byte[] ReadBytes(string path)
        {
            return File.ReadAllBytes(path);
        }
    }
}
