using System.IO;
using System.Threading.Tasks;

namespace Sentro.Utilities
{
    /*
        Responsibility : group all Hard drive writing opreations together
        TODO : remove this class with refactoring
    */
    static class Writer
    {
        public static void Write(string text, StreamWriter file)
        {
            file.WriteLine(text);
        }
        public static async void WriteAsync(string text, StreamWriter file)
        {
            await file.WriteLineAsync(text);
        }
        public static void Write(byte[] bytes, StreamWriter file)
        {
            file.BaseStream.Write(bytes,0,bytes.Length);
        }
        public static async void WriteAsync(byte[] bytes, StreamWriter file)
        {
            await file.BaseStream.WriteAsync(bytes, 0, bytes.Length);
        }
        public static void Write(string text, string path)
        {
            File.WriteAllText(path,text);
        }
        public static async void WriteAsync(string text, string path)
        {
            await Task.Run(() =>
                File.WriteAllText(path, text));
        }
        public static void Write(byte[] bytes, string path)
        {
            File.WriteAllBytes(path,bytes);
        }
        public static async void WriteAsync(byte[] bytes, string path)
        {
            await Task.Run(() =>
                File.WriteAllBytes(path, bytes));
        }        
    }
}
