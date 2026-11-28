using Monk.Audio;
using Monk.Gameplay;
using UnityEngine;

namespace Monk.World
{
    public enum DoorStyle { SlideSideways, Rise }

    /// <summary>
    /// Heavy industrial door. Closing is never instant: it first warns (sparks, beeping, flashing lamps), then drops
    /// with weight. A player whose body is under the panel while it closes is crushed.
    /// Automatic doors cycle on their own; every door can also be toggled from its buttons.
    /// </summary>
    public class IndustrialDoor : MonoBehaviour
    {
        public enum State { Open, Warning, Closing, Closed, Opening }

        [Header("Setup")]
        public Transform panel;
        public DoorStyle style;
        [Tooltip("Local-space offset from the closed position to the open position.")]
        public Vector3 openOffset = new Vector3(3.9f, 0f, 0f);
        public bool startOpen = true;
        public Collider panelCollider;
        public Renderer[] warningLamps;
        public Material sparkMaterial;

        [Header("Behaviour")]
        public bool automatic;
        public float autoOpenSeconds = 7f;
        public float autoClosedSeconds = 4f;
        public float warningSeconds = 1.6f;
        public float closeSeconds = 0.8f;
        public float openSeconds = 2.0f;
        public float crushDamage = 100f;

        public State Current { get; private set; }

        private Vector3 _closedPos, _openPos;
        private float _timer, _t, _beepTimer;
        private ParticleSystem _sparks;
        private AudioSource _audio, _loop;
        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _closedPos = panel.localPosition;
            _openPos = _closedPos + openOffset;
            Current = startOpen ? State.Open : State.Closed;
            panel.localPosition = startOpen ? _openPos : _closedPos;

            _audio = panel.gameObject.AddComponent<AudioSource>();
            ConfigureSource(_audio, false, 1.5f, 28f);

            _loop = panel.gameObject.AddComponent<AudioSource>();
            ConfigureSource(_loop, true, 1.5f, 18f);
            _loop.clip = ProceduralAudio.Rumble();
            _loop.volume = 0.5f;

            _mpb = new MaterialPropertyBlock();
            BuildSparks();
            SetLamps(0f);
        }

        private static void ConfigureSource(AudioSource s, bool loop, float min, float max)
        {
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 1f;
            s.minDistance = min;
            s.maxDistance = max;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = 0f;
        }

        private void BuildSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            go.transform.position = panel.position + (style == DoorStyle.Rise ? Vector3.up * 0.8f : Vector3.up * 1.2f);
            _sparks = go.AddComponent<ParticleSystem>();
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _sparks.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = 0.05f;
            main.startColor = new Color(1f, 0.75f, 0.25f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _sparks.emission;
            emission.enabled = false; // we Emit() manually

            var shape = _sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.5f, 0.1f, 0.3f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (sparkMaterial != null) r.sharedMaterial = sparkMaterial;
        }

        // ---------------------------------------------------------------- control

        /// <summary>Open if closed; start the warning and close if open. Ignored mid-motion.</summary>
        public void Toggle()
        {
            if (Current == State.Open) RequestClose();
            else if (Current == State.Closed) BeginOpen();
        }

        public void ForceOpen()
        {
            if (Current == State.Closed) BeginOpen();
            else if (Current == State.Warning) { Current = State.Open; SetLamps(0f); }
        }

        private void RequestClose()
        {
            Current = State.Warning;
            _timer = 0f;
            _beepTimer = 0f;
        }

        private void BeginOpen()
        {
            Current = State.Opening;
            _t = 0f;
            _loop.Play();
            _audio.PlayOneShot(ProceduralAudio.Clank(), 0.5f);
        }

        // ----------------------------------------------------------------- update

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (Current)
            {
                case State.Open:
                    if (automatic)
                    {
                        _timer += dt;
                        if (_timer >= autoOpenSeconds) RequestClose();
                    }
                    break;

                case State.Warning:
                    _timer += dt;
                    _beepTimer -= dt;
                    if (_beepTimer <= 0f)
                    {
                        _beepTimer = 0.25f;
                        _audio.PlayOneShot(ProceduralAudio.Beep(), 0.8f);
                        _sparks.Emit(6);
                    }
                    SetLamps(Mathf.Sin(_timer * 28f) > 0f ? 1f : 0.1f);
                    if (_timer >= warningSeconds)
                    {
                        Current = State.Closing;
                        _t = 0f;
                        _loop.Play();
                    }
                    break;

                case State.Closing:
                    _t += dt / closeSeconds;
                    float fall = Mathf.Clamp01(_t);
                    panel.localPosition = Vector3.Lerp(_openPos, _closedPos, fall * fall); // accelerates: heavy
                    SetLamps(1f);
                    CrushCheck();
                    if (_t >= 1f)
                    {
                        Current = State.Closed;
                        _timer = 0f;
                        _loop.Stop();
                        _audio.PlayOneShot(ProceduralAudio.Slam(), 1f);
                        _sparks.Emit(25);
                        NoiseBus.Emit(panel.position, 1f);
                        SetLamps(0f);
                    }
                    break;

                case State.Closed:
                    if (automatic)
                    {
                        _timer += dt;
                        if (_timer >= autoClosedSeconds) BeginOpen();
                    }
                    break;

                case State.Opening:
                    _t += dt / openSeconds;
                    float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t));
                    panel.localPosition = Vector3.Lerp(_closedPos, _openPos, rise);
                    if (_t >= 1f)
                    {
                        Current = State.Open;
                        _timer = 0f;
                        _loop.Stop();
                    }
                    break;
            }
        }

        private void CrushCheck()
        {
            var health = PlayerHealth.Instance;
            if (health == null || panelCollider == null) return;
            if (health.Overlaps(panelCollider.bounds)) health.Damage(crushDamage, "Crushed by a door");
        }

        private void SetLamps(float level)
        {
            if (warningLamps == null) return;
            _mpb.SetColor(EmissionId, new Color(1f, 0.1f, 0.05f) * (level * 3f));
            foreach (var r in warningLamps)
                if (r != null) r.SetPropertyBlock(_mpb);
        }
    }
}
