using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SentroTest
{
    [TestClass]
    public class Murmur2Tests
    {
        [TestMethod]
        public void SimpleHashingWithString()
        {
            var url = @"www.google.com/?q=how+to+hash+urls";
            var hashUint = Sentro.Utilities.Murmur2.Hash(url,Encoding.ASCII);
            var hashString = Sentro.Utilities.Murmur2.HashX8(url, Encoding.ASCII);
            Assert.AreEqual(hashString,"A21CAA15");
            Assert.AreEqual(hashUint, 2719787541);
        }
    }
}
