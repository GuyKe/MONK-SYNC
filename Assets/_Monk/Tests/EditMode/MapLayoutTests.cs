using System.Collections.Generic;
using Monk.World;
using NUnit.Framework;
using UnityEngine;

namespace Monk.Tests
{
    public class MapLayoutTests
    {
        [SetUp]
        public void Generate() => MapLayout.Generate(MapLayout.DefaultSeed);

        private static int CountWalkable()
        {
            int n = 0;
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    if (MapLayout.IsWalkable(MapLayout.At(x, z))) n++;
            return n;
        }

        [Test]
        public void MapIsBig()
        {
            Assert.GreaterOrEqual(MapLayout.Width, 40);
            Assert.GreaterOrEqual(CountWalkable(), 1500);
        }

        [Test]
        public void HubSitsDirectlyAboveTheAtrium()
        {
            var hub = MapLayout.HubRect;
            var atrium = MapLayout.AtriumRect;
            Assert.AreEqual(atrium.x, hub.x);
            Assert.AreEqual(atrium.z + atrium.h, hub.z);
            for (int x = hub.x; x < hub.x + hub.w; x++)
            {
                Assert.AreEqual(' ', MapLayout.At(x, hub.z), "hub block is solid");
                Assert.AreEqual('H', MapLayout.At(x, atrium.z + atrium.h - 1), "atrium row below the hub");
                Assert.IsTrue(MapLayout.NeedsWall(x, atrium.z + atrium.h - 1, 0), "the hub facade is a wall");
            }
        }

        [Test]
        public void AtriumHasDoorwaysIntoTheMaze()
        {
            var a = MapLayout.AtriumRect;
            int openings = 0;
            for (int z = a.z; z < a.z + a.h; z++)
                if (!MapLayout.NeedsWall(a.x, z, 3) || !MapLayout.NeedsWall(a.x + a.w - 1, z, 1)) openings++;
            for (int x = a.x; x < a.x + a.w; x++)
                if (!MapLayout.NeedsWall(x, a.z, 2)) openings++;
            Assert.GreaterOrEqual(openings, 4);
        }

        [Test]
        public void EveryWalkableCellIsReachableFromTheAtrium()
        {
            Assert.AreEqual(CountWalkable(), MapLayout.ReachableFrom(MapLayout.AtriumCentre).Count);
        }

        [Test]
        public void ExitExistsAndIsADeadEnd()
        {
            Assert.AreEqual('E', MapLayout.At(MapLayout.ExitCell.x, MapLayout.ExitCell.y));
            Assert.AreEqual(1, MapLayout.PassageCount(MapLayout.ExitCell.x, MapLayout.ExitCell.y));
        }

        [Test]
        public void FireDoorsAreInStraightCorridors()
        {
            int doors = 0;
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    if (MapLayout.IsDoor(MapLayout.At(x, z)))
                    {
                        doors++;
                        Assert.AreEqual(2, MapLayout.PassageCount(x, z), $"door at {x},{z} should connect exactly two cells");
                    }
            Assert.Greater(doors, 10);
        }

        [Test]
        public void LockedDoorsNeverCutTheMapInTwo()
        {
            var doors = new List<Vector2Int>();
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    if (MapLayout.At(x, z) == 'D' && MapLayout.RoomIdAt(x, z) < 0) doors.Add(new Vector2Int(x, z));

            var locked = MapLayout.ChooseLockableDoors(doors, 6);
            Assert.GreaterOrEqual(locked.Count, 3, "enough shortcut doors to lock");
            var blocked = new HashSet<Vector2Int>(locked);
            Assert.AreEqual(CountWalkable() - blocked.Count, MapLayout.ReachableFrom(MapLayout.AtriumCentre, blocked).Count);
        }

        [Test]
        public void PitCellsOnlyTouchPitSpaceLedgesOrOtherCellsThroughWalls()
        {
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                {
                    if (!MapLayout.IsPitSpace(MapLayout.At(x, z))) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        char n = MapLayout.At(x + MapLayout.DX[d], z + MapLayout.DZ[d]);
                        if (MapLayout.IsPitSpace(n) || n == 'o') continue;
                        Assert.IsTrue(MapLayout.NeedsWall(x, z, d), $"pit cell {x},{z} is open to '{n}'");
                    }
                }
        }

        [Test]
        public void SeveralSeedsAllGenerateValidMaps()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                MapLayout.Generate(seed);
                Assert.AreEqual(CountWalkable(), MapLayout.ReachableFrom(MapLayout.AtriumCentre).Count, "seed " + seed);
            }
        }
    }
}
