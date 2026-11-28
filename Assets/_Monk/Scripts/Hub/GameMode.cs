using System;

namespace Monk.Hub
{
    public enum GameMode { StandardEscape, Infection, Blackout, Speedrun }

    /// <summary>The selected gamemode. Gamemodes themselves come later; for now the hub reacts to the choice.</summary>
    public static class GameModeState
    {
        public static GameMode Current { get; private set; } = GameMode.StandardEscape;
        public static event Action<GameMode> Changed;

        public static void Set(GameMode mode)
        {
            if (mode == Current) return;
            Current = mode;
            Changed?.Invoke(mode);
        }

        public static string Title(GameMode m)
        {
            switch (m)
            {
                case GameMode.Infection: return "INFECTION / TAG";
                case GameMode.Blackout: return "LIGHTS OUT";
                case GameMode.Speedrun: return "SPEEDRUN";
                default: return "STANDARD ESCAPE";
            }
        }

        public static string Description(GameMode m)
        {
            switch (m)
            {
                case GameMode.Infection: return "ONE PLAYER IS INFECTED.\nTAG OTHERS BEFORE THE\nTIMER RUNS OUT.";
                case GameMode.Blackout: return "THE POWER IS OUT.\nFIND THE FUSE BOXES\nIN THE DARK.";
                case GameMode.Speedrun: return "REACH THE EXIT SEAM\nAS FAST AS YOU CAN.\nTHE CLOCK IS RUNNING.";
                default: return "FIND THE EXIT SEAM\nAND ESCAPE LEVEL 0\nALIVE.";
            }
        }
    }
}
