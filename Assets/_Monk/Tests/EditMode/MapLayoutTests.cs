using Monk.World;
using NUnit.Framework;
using UnityEngine;

namespace Monk.Tests
{
    public class MapLayoutTests
    {
        [Test]
        public void AllRowsAreTheSameWidth()
        {
            foreach (var row in MapLayout.Rows) Assert.AreEqual(MapLayout.Rows[0].Length, row.Length);
        }

        [Test]
        public void StartAndExitAreWalkable()
        {
            Assert.IsTrue(MapLayout.IsWalkable(MapLayout.At(MapLayout.StartCell.x, MapLayout.StartCell.y)));
            Assert.AreEqual('E', MapLayout.At(MapLayout.ExitCell.x, MapLayout.ExitCell.y));
        }

        [Test]
        public void EveryWalkableCellIsReachableFromStart()
        {
            var reach = MapLayout.ReachableFrom(MapLayout.StartCell);
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                    if (MapLayout.IsWalkable(MapLayout.At(x, z)))
                        Assert.IsTrue(reach.Contains(new Vector2Int(x, z)), $"cell {x},{z} ('{MapLayout.At(x, z)}') is unreachable");
        }

        [Test]
        public void PitCellsOnlyTouchPitSpaceLedgesOrSolid()
        {
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                {
                    if (!MapLayout.IsPitSpace(MapLayout.At(x, z))) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        char n = MapLayout.At(x + MapLayout.DX[d], z + MapLayout.DZ[d]);
                        Assert.IsTrue(n == ' ' || n == 'o' || MapLayout.IsPitSpace(n), $"pit cell {x},{z} touches '{n}'");
                    }
                }
        }

        [Test]
        public void DoorsHaveWalkableCellsOnBothSidesOfTheirAxis()
        {
            for (int z = 0; z < MapLayout.Height; z++)
                for (int x = 0; x < MapLayout.Width; x++)
                {
                    if (!MapLayout.IsDoor(MapLayout.At(x, z))) continue;
                    bool alongZ = MapLayout.DoorRunsAlongZ(x, z);
                    int dx = alongZ ? 0 : 1, dz = alongZ ? 1 : 0;
                    Assert.IsTrue(MapLayout.IsWalkable(MapLayout.At(x + dx, z + dz)) && MapLayout.IsWalkable(MapLayout.At(x - dx, z - dz)),
                        $"door at {x},{z} does not connect two cells");
                }
        }
    }
}
