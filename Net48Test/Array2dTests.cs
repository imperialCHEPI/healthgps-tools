using HealthGps.R;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HealthGps.Net48Test
{
    [TestClass]
    public class Array2dTests
    {
        [TestMethod]
        public void CreateEmpty()
        {
            var a = new Array2D<int>();
            var b = new Array2D<int>(0, 0);

            Assert.AreEqual(0, a.Count);
            Assert.AreEqual(0, b.Count);
            Assert.AreEqual(a, b);
        }

        [TestMethod]
        public void CompareForEquality()
        {
            var a = new Array2D<int>(3, 3, 5);
            var b = new Array2D<int>(3, 3, 5);
            var c = new Array2D<int>(3, 3, 7);

            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.Count, c.Count);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
            Assert.AreNotEqual(b, c);
        }

        [TestMethod]
        public void HashcodeIsUnique()
        {
            var a = new Array2D<int>(3, 3, 5);
            var b = new Array2D<int>(3, 3, 5);
            var c = new Array2D<int>(3, 3, 7);

            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a.GetHashCode(), c.GetHashCode());
            Assert.AreNotEqual(b.GetHashCode(), c.GetHashCode());
            Assert.AreEqual(c.GetHashCode(), c.GetHashCode());
        }
    }
}
