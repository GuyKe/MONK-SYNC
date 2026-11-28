using System.Text;
using Monk.Audio;
using Monk.Cosmetics;
using Monk.World;
using UnityEngine;

namespace Monk.Hub
{
    /// <summary>
    /// The retro terminal. Page 1 edits your display name with the physical keyboard; page 2 shows server stats.
    /// </summary>
    public class TerminalScreen : MonoBehaviour
    {
        public TextMesh text;
        public int mapSeed;

        private readonly StringBuilder _buffer = new StringBuilder();
        private bool _statsPage;
        private string _flash;
        private float _flashUntil;
        private float _refresh;

        private void Start()
        {
            _buffer.Append(PlayerProfile.DisplayName);
            Redraw();
        }

        private void Update()
        {
            _refresh -= Time.deltaTime;
            if (_refresh > 0f) return;
            _refresh = 0.4f;
            Redraw();
        }

        public void TypeChar(char c)
        {
            if (_statsPage) return;
            if (_buffer.Length >= PlayerProfile.MaxNameLength) { Deny(); return; }
            _buffer.Append(char.ToUpperInvariant(c));
            Redraw();
        }

        public void Backspace()
        {
            if (_statsPage) return;
            if (_buffer.Length > 0) _buffer.Length--;
            Redraw();
        }

        public void Enter()
        {
            if (_statsPage) { _statsPage = false; Redraw(); return; }
            if (_buffer.ToString().Trim().Length == 0) { Deny(); return; }
            PlayerProfile.DisplayName = _buffer.ToString();
            _buffer.Length = 0;
            _buffer.Append(PlayerProfile.DisplayName);
            Flash("NAME SAVED");
        }

        public void TogglePage()
        {
            _statsPage = !_statsPage;
            Redraw();
        }

        private void Deny()
        {
            AudioSource.PlayClipAtPoint(ProceduralAudio.Deny(), transform.position, 0.5f);
            Flash("INVALID");
        }

        private void Flash(string message)
        {
            _flash = message;
            _flashUntil = Time.time + 1.5f;
            Redraw();
        }

        private void Redraw()
        {
            if (text == null) return;
            bool flashing = Time.time < _flashUntil;
            var sb = new StringBuilder();
            sb.AppendLine("== ASYNC FACILITY TERMINAL v2.3 ==");
            sb.AppendLine();
            if (!_statsPage)
            {
                bool cursor = (int)(Time.time * 2f) % 2 == 0;
                sb.AppendLine("OPERATOR NAME:");
                sb.AppendLine("> " + _buffer + (cursor ? "_" : " "));
                sb.AppendLine();
                sb.AppendLine(flashing ? "[ " + _flash + " ]" : "TAP KEYS TO TYPE");
                sb.AppendLine("ENTER = SAVE   STATS = PAGE");
                sb.AppendLine("SAVED AS: " + PlayerProfile.DisplayName);
            }
            else
            {
                int t = (int)Time.realtimeSinceStartup;
                int locked = 0;
                foreach (var d in FindObjectsByType<IndustrialDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (d.locked) locked++;
                sb.AppendLine("SERVER    LOBBY-01");
                sb.AppendLine($"UPTIME    {t / 3600:00}:{t / 60 % 60:00}:{t % 60:00}");
                sb.AppendLine("PLAYERS   1 / 12");
                sb.AppendLine("PING      " + (18 + (int)(Mathf.PerlinNoise(Time.time * 0.5f, 1f) * 14f)) + " MS");
                sb.AppendLine("MODE      " + GameModeState.Title(GameModeState.Current));
                sb.AppendLine("MAP       LEVEL 0  SEED " + mapSeed);
                sb.AppendLine("DOORS     " + locked + " LOCKED");
                sb.AppendLine(flashing ? "[ " + _flash + " ]" : "STATS = BACK");
            }
            text.text = sb.ToString();
        }
    }
}
