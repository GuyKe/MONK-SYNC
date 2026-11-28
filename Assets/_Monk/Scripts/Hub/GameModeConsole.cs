using UnityEngine;

namespace Monk.Hub
{
    /// <summary>The gamemode console's screens and lights. Reacts to GameModeState and the two levers.</summary>
    public class GameModeConsole : MonoBehaviour
    {
        public TextMesh titleText;
        public TextMesh descriptionText;
        public TextMesh statusText;
        public TextMesh optionsText;
        [Tooltip("One lamp ring per mode, in GameMode order.")]
        public Renderer[] modeLamps;

        public bool AlarmMuted { get; private set; }
        public int PrepSeconds { get; private set; } = 30;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color[] ModeColors =
        {
            new Color(0.2f, 0.9f, 1f), new Color(1f, 0.1f, 0.08f), new Color(1f, 0.6f, 0.05f), new Color(0.2f, 1f, 0.3f),
        };

        private MaterialPropertyBlock _mpb;

        private void OnEnable()
        {
            _mpb = new MaterialPropertyBlock();
            GameModeState.Changed += Refresh;
            Refresh(GameModeState.Current);
        }

        private void OnDisable() => GameModeState.Changed -= Refresh;

        public void SetAlarmMuted(bool muted)
        {
            AlarmMuted = muted;
            Refresh(GameModeState.Current);
        }

        public void CyclePrepTime()
        {
            PrepSeconds = PrepSeconds == 30 ? 60 : PrepSeconds == 60 ? 90 : 30;
            Refresh(GameModeState.Current);
        }

        private void Refresh(GameMode mode)
        {
            if (titleText != null) titleText.text = GameModeState.Title(mode);
            if (descriptionText != null) descriptionText.text = GameModeState.Description(mode);
            if (statusText != null) statusText.text = "LOBBY OPEN\nPLAYERS 1 / 12\nREADY 0 / 1";
            if (optionsText != null) optionsText.text = $"PREP TIME  {PrepSeconds}s\nALARM  {(AlarmMuted ? "MUTED" : "ON")}";

            if (modeLamps == null) return;
            for (int i = 0; i < modeLamps.Length; i++)
            {
                if (modeLamps[i] == null) continue;
                bool on = i == (int)mode;
                _mpb.SetColor(EmissionId, ModeColors[Mathf.Min(i, ModeColors.Length - 1)] * (on ? 3.5f : 0.15f));
                modeLamps[i].SetPropertyBlock(_mpb);
            }
        }
    }
}
