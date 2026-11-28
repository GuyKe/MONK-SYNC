using System.Collections.Generic;
using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// The map as data. One character per 4 m cell; Rows[z][x], row 0 is the south edge.
    ///   .  carpet hall            ,  tall carpet hall (catwalk)      F  flooded hall
    ///   S  start zone (concrete)  X  start zone with window (east)   A  airlock door
    ///   D  fire door (manual)     B  blast door (automatic)          R  server / relay station
    ///   o  pit-room ledge         p  pit                             =  walkway along X
    ///   |  walkway along Z        +  walkway junction                E  exit
    ///   (space) solid
    /// </summary>
    public static class MapLayout
    {
        public const float Cell = 4f;
        public const float PitDepth = 6f;

        public static readonly string[] Rows =
        {
            "   SSSSSS...... ", // z00
            "   SSSSSX...... ", // z01
            "   SSSSSX...... ", // z02
            "   SSSSSS...... ", // z03
            "      A  ...... ", // z04
            " ...............", // z05
            " .  F  . .     .", // z06
            " .  F  . .     .", // z07
            " .  F  . .     .", // z08
            " .  F  B o=o=o .", // z09
            " .D.F  . |p|p| .", // z10
            " .  F  . +=+=+ .", // z11
            " .  F  . |ppp| .", // z12
            " .  F  . o===o .", // z13
            " .,,,,,..    B .", // z14
            " .     . .......", // z15
            " .     .    D   ", // z16
            " .     .  RRRRR ", // z17
            " ........ RRRRR ", // z18
            " E        RRRRR ", // z19
        };

        public static readonly Vector2Int StartCell = new Vector2Int(5, 1);
        public static readonly Vector2Int ExitCell = new Vector2Int(1, 19);

        public static readonly int[] DX = { 0, 1, 0, -1 }; // N, E, S, W
        public static readonly int[] DZ = { 1, 0, -1, 0 };

        public static int Width => Rows[0].Length;
        public static int Height => Rows.Length;

        public static char At(int x, int z) =>
            (x < 0 || z < 0 || z >= Rows.Length || x >= Rows[z].Length) ? ' ' : Rows[z][x];

        public static bool IsFloor(char c) => c != ' ';
        public static bool IsWalkable(char c) => c != ' ' && c != 'p';
        public static bool IsPitSpace(char c) => c == 'p' || c == '=' || c == '|' || c == '+';
        public static bool IsDoor(char c) => c == 'A' || c == 'D' || c == 'B';

        /// <summary>0 = the maze, 1 = start zone, 2 = server room. Zones are sealed apart except through doors.</summary>
        public static int Zone(char c) => (c == 'S' || c == 'X') ? 1 : (c == 'R' ? 2 : 0);

        public static float CeilingHeight(char c)
        {
            switch (c)
            {
                case 'S': case 'X': case 'A': return 4.2f;
                case ',': return 5.2f;
                case 'R': return 3.0f;
                case 'o': case 'p': case '=': case '|': case '+': return 6.4f;
                default: return 3.2f;
            }
        }

        public static Vector3 CellCenter(int x, int z) => new Vector3((x + 0.5f) * Cell, 0f, (z + 0.5f) * Cell);

        /// <summary>True if a door cell's passage runs along Z (the door panel then spans X).</summary>
        public static bool DoorRunsAlongZ(int x, int z) =>
            IsWalkable(At(x, z + 1)) && IsWalkable(At(x, z - 1));

        private static bool AllowsDirection(char c, int x, int z, int dx, int dz)
        {
            if (c == '=') return dz == 0;
            if (c == '|') return dx == 0;
            if (IsDoor(c)) return DoorRunsAlongZ(x, z) ? dx == 0 : dz == 0;
            return true;
        }

        /// <summary>Can a player walk from (x,z) one step in (dx,dz)?</summary>
        public static bool Open(int x, int z, int dx, int dz)
        {
            char a = At(x, z), b = At(x + dx, z + dz);
            if (!IsWalkable(a) || !IsWalkable(b)) return false;
            if (!AllowsDirection(a, x, z, dx, dz) || !AllowsDirection(b, x + dx, z + dz, -dx, -dz)) return false;
            if (Zone(a) != Zone(b) && !IsDoor(a) && !IsDoor(b)) return false;
            return true;
        }

        /// <summary>Does the edge of cell (x,z) in direction dir (0=N,1=E,2=S,3=W) need a wall?</summary>
        public static bool NeedsWall(int x, int z, int dir)
        {
            char a = At(x, z), b = At(x + DX[dir], z + DZ[dir]);
            if (!IsFloor(b)) return true;
            if (IsPitSpace(a) && IsPitSpace(b)) return false;
            return !Open(x, z, DX[dir], DZ[dir]);
        }

        public static HashSet<Vector2Int> ReachableFrom(Vector2Int start)
        {
            var seen = new HashSet<Vector2Int> { start };
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                for (int d = 0; d < 4; d++)
                {
                    if (!Open(p.x, p.y, DX[d], DZ[d])) continue;
                    var n = new Vector2Int(p.x + DX[d], p.y + DZ[d]);
                    if (seen.Add(n)) stack.Push(n);
                }
            }
            return seen;
        }
    }
}
