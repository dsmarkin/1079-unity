using NUnit.Framework;
using UnityEngine;
using Height1079.Snow;

namespace Height1079.Tests
{
    /// <summary>The snow underfoot, shared by the hiker and the sandbox body: the rule for how deep a boot goes on a
    /// path, and the picture of the print. The component itself needs a running scene (its prints are objects), so
    /// what is checked here is what both bodies rely on without one.</summary>
    public class SnowPrintsTests
    {
        [Test]
        public void VirginSnowTakesTheWholeDepth()
        {
            Assert.AreEqual(1f, SnowPrints.SinkFactor(0));
        }

        [Test]
        public void ATroddenPathIsShallowerAndStaysThatWay()
        {
            Assert.Less(SnowPrints.SinkFactor(1), SnowPrints.SinkFactor(0));
            Assert.Less(SnowPrints.SinkFactor(3), SnowPrints.SinkFactor(1));
            Assert.AreEqual(SnowPrints.SinkFactor(3), SnowPrints.SinkFactor(12));
        }

        [Test]
        public void ThePrintHasASoleAndAClearEdge()
        {
            var t = SnowPrintArt.Footprint();
            try
            {
                Assert.Greater(t.GetPixel(t.width / 2, t.height / 2).a, .5f, "the sole is drawn");
                Assert.AreEqual(0f, t.GetPixel(0, 0).a, 1e-3f, "the corner is clear");
            }
            finally { Object.DestroyImmediate(t); }
        }
    }
}
