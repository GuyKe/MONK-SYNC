using System;
using UnityEngine;

namespace Monk.Cosmetics
{
    /// <summary>The player's display name, saved on this device.</summary>
    public static class PlayerProfile
    {
        public const int MaxNameLength = 12;
        private const string Key = "monk.name";
        private static string _name;

        public static event Action<string> NameChanged;

        public static string DisplayName
        {
            get
            {
                if (_name == null) _name = PlayerPrefs.GetString(Key, "SURVIVOR");
                return _name;
            }
            set
            {
                string v = (value ?? "").Trim().ToUpperInvariant();
                if (v.Length == 0) v = "SURVIVOR";
                if (v.Length > MaxNameLength) v = v.Substring(0, MaxNameLength);
                _name = v;
                PlayerPrefs.SetString(Key, v);
                PlayerPrefs.Save();
                NameChanged?.Invoke(v);
            }
        }
    }
}
