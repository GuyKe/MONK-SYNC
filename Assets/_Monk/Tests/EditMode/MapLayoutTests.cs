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
        public void ManyIdenticalStartRooms()
        {
            int starts = 0;
            foreach (var r in MapLayout.Rooms)
            {
                if (r.kind != RoomKind.Start) continue;
                starts++;
                Assert.AreEqual('A', MapLayout.At(r.rect.x + 2, r.rect.z + 4), "airlock in the same place in every start room");
                Assert.AreEqual('X', MapLayout.At(r.rect.x + 5, r.rect.z + 1), "window in the same place in every start room");
            }
            Assert.GreaterOrEqual(starts, 5);
        }

        [Test]
        public void EveryWalkableCellIsReachableFromEveryStartRoom()
        {
            int walkable = CountWalkable();
            foreach (var r in MapLayout.Rooms)
                if (r.kind == RoomKind.Start)
                    Assert.AreEqual(walkable, MapLayout.ReachableFrom(r.Spawn).Count, "start room at " + r.rect.x + "," + r.rect.z);
        }

        [Test]
        public void ExitExistsAndIsADeadEnd()
        {
            Assert.AreEqual('E', MapLayout.At(MapLayout.ExitCell.x, MapLayout.ExitCell.y));
            Assert.AreEqual(1, MapLayout.PassageCount(MapLayout.ExitCell.x, MapLayout.ExitCell.y));
        }

        [Test]
        public void DoorsAreInStraightCorridors()
        {
            int doors = 0;
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    if (MapLayout.IsDoor(MapLayout.At(x, z)))
                    {
                        doors++;
                        Assert.AreEqual(2, MapLayout.PassageCount(x, z), $"door at {x},{z} should connect exactly two cells");
                    }
            Assert.Greater(doors, 8);
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
                int walkable = CountWalkable();
                foreach (var r in MapLayout.Rooms)
                    if (r.kind == RoomKind.Start)
                        Assert.AreEqual(walkable, MapLayout.ReachableFrom(r.Spawn).Count, "seed " + seed);
            }
        }
    }
}
