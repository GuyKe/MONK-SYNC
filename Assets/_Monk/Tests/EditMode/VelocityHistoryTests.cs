using Monk.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace Monk.Tests
{
    public class VelocityHistoryTests
    {
        [Test]
        public void EmptyAverageIsZero()
        {
            Assert.AreEqual(Vector3.zero, new VelocityHistory(4).Average());
        }

        [Test]
        public void AveragesPartialBuffer()
        {
            var h = new VelocityHistory(4);
            h.Push(new Vector3(2, 0, 0));
            h.Push(new Vector3(4, 0, 0));
            Assert.AreEqual(2, h.Count);
            Assert.AreEqual(3f, h.Average().x, 1e-5f);
        }

        [Test]
        public void OverwritesOldestWhenFull()
        {
            var h = new VelocityHistory(2);
            h.Push(new Vector3(100, 0, 0));
            h.Push(new Vector3(2, 0, 0));
            h.Push(new Vector3(4, 0, 0));
            Assert.AreEqual(2, h.Count);
            Assert.AreEqual(3f, h.Average().x, 1e-5f);
        }

        [Test]
        public void ClearResets()
        {
            var h = new VelocityHistory(3);
            h.Push(Vector3.one);
            h.Clear();
            Assert.AreEqual(0, h.Count);
            Assert.AreEqual(Vector3.zero, h.Average());
        }
    }
}
