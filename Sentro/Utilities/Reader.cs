using System.IO;

namespace Sentro.Utilities
{
    static class Reader
    {
        public static byte[] ReadBytes(string path)
        {
            return File.ReadAllBytes(path);
        }
    }
}
