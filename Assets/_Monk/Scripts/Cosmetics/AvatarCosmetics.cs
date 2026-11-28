using System;
using UnityEngine;

namespace Monk.Cosmetics
{
    /// <summary>
    /// The player's look: a hazmat suit colour, a hat, a visor and a badge. Saved on this device.
    /// The head, hat, visor and name tag live on a hidden layer: you never see them from inside your own head, but
    /// the wardrobe mirror (and other players, later) do.
    /// </summary>
    public class AvatarCosmetics : MonoBehaviour
    {
        public Transform head;
        public Transform torso;
        [Tooltip("Everything that takes the suit colour: torso, arms, hand visuals.")]
        public Renderer[] suitRenderers;
        [Tooltip("Arms are line renderers, which need an unlit material.")]
        public Renderer[] armRenderers;
        public Material armMaterial;
        public Material suitMaterial;
        public Material accessoryMaterial;
        [Tooltip("Index = VisorKind. Element 0 is unused.")]
        public Material[] visorMaterials;

        public int Hat { get; private set; }
        public int Visor { get; private set; }
        public int Badge { get; private set; }
        public int Suit { get; private set; }
        public event Action Changed;

        private Material _suit, _arm;
        private Transform _hatRoot, _visorRoot, _badgeAnchor;
        private GameObject _hat, _visor, _badge;
        private TextMesh _nameTag;

        private void Start()
        {
            Hat = PlayerPrefs.GetInt("monk.cos.hat", 0);
            Visor = PlayerPrefs.GetInt("monk.cos.visor", 1);
            Badge = PlayerPrefs.GetInt("monk.cos.badge", 0);
            Suit = PlayerPrefs.GetInt("monk.cos.suit", 0);
            Clamp();

            _suit = new Material(suitMaterial);
            foreach (var r in suitRenderers) if (r != null) r.sharedMaterial = _suit;
            if (armMaterial != null)
            {
                _arm = new Material(armMaterial);
                foreach (var r in armRenderers) if (r != null) r.sharedMaterial = _arm;
            }

            var cam = head.GetComponent<Camera>();
            if (cam != null) cam.cullingMask &= ~(1 << CosmeticModels.HiddenLayer);

            // Head, neck and name tag, hidden from your own camera.
            var headMesh = CosmeticModels.Prim(PrimitiveType.Sphere, head, new Vector3(0f, 0f, -0.02f), Vector3.one * 0.26f, suitMaterial, Color.white, CosmeticModels.HiddenLayer);
            headMesh.GetComponent<MeshRenderer>().sharedMaterial = _suit;
            CosmeticModels.Prim(PrimitiveType.Cylinder, head, new Vector3(0f, -0.2f, -0.02f), new Vector3(0.1f, 0.08f, 0.1f), suitMaterial, Color.white, CosmeticModels.HiddenLayer)
                .GetComponent<MeshRenderer>().sharedMaterial = _suit;

            _hatRoot = new GameObject("HatRoot") { layer = CosmeticModels.HiddenLayer }.transform;
            _hatRoot.SetParent(head, false);
            _hatRoot.localPosition = new Vector3(0f, 0.11f, -0.02f);
            _visorRoot = new GameObject("VisorRoot") { layer = CosmeticModels.HiddenLayer }.transform;
            _visorRoot.SetParent(head, false);
            _visorRoot.localPosition = new Vector3(0f, 0f, -0.02f);

            _badgeAnchor = new GameObject("BadgeAnchor").transform;
            _badgeAnchor.SetParent(transform, false);

            var tagGo = new GameObject("NameTag") { layer = CosmeticModels.HiddenLayer };
            tagGo.transform.SetParent(head, false);
            tagGo.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            tagGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _nameTag = tagGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _nameTag.font = font;
            tagGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            _nameTag.anchor = TextAnchor.MiddleCenter;
            _nameTag.characterSize = 0.025f;
            _nameTag.fontSize = 64;
            _nameTag.color = Color.white;

            PlayerProfile.NameChanged += OnName;
            OnName(PlayerProfile.DisplayName);
            Apply();
        }

        private void OnDestroy() => PlayerProfile.NameChanged -= OnName;

        private void OnName(string n)
        {
            if (_nameTag != null) _nameTag.text = n;
        }

        private void LateUpdate()
        {
            if (_badgeAnchor == null || torso == null) return;
            _badgeAnchor.position = torso.position + torso.rotation * new Vector3(0.09f, 0.12f, 0.1f);
            _badgeAnchor.rotation = torso.rotation;
        }

        private void Clamp()
        {
            Hat = Mathf.Clamp(Hat, 0, CosmeticModels.HatNames.Length - 1);
            Visor = Mathf.Clamp(Visor, 0, CosmeticModels.VisorNames.Length - 1);
            Badge = Mathf.Clamp(Badge, 0, CosmeticModels.BadgeNames.Length - 1);
            Suit = Mathf.Clamp(Suit, 0, CosmeticModels.SuitNames.Length - 1);
        }

        public int Get(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return Hat;
                case CosmeticSlot.Visor: return Visor;
                case CosmeticSlot.Badge: return Badge;
                default: return Suit;
            }
        }

        public void Set(CosmeticSlot slot, int value)
        {
            int n = CosmeticModels.Count(slot);
            value = ((value % n) + n) % n;
            switch (slot)
            {
                case CosmeticSlot.Hat: Hat = value; break;
                case CosmeticSlot.Visor: Visor = value; break;
                case CosmeticSlot.Badge: Badge = value; break;
                default: Suit = value; break;
            }
            PlayerPrefs.SetInt("monk.cos.hat", Hat);
            PlayerPrefs.SetInt("monk.cos.visor", Visor);
            PlayerPrefs.SetInt("monk.cos.badge", Badge);
            PlayerPrefs.SetInt("monk.cos.suit", Suit);
            PlayerPrefs.Save();
            Apply();
        }

        public void Cycle(CosmeticSlot slot, int step) => Set(slot, Get(slot) + step);

        private void Apply()
        {
            if (_suit == null) return;
            _suit.color = CosmeticModels.SuitColors[Suit];
            if (_arm != null) _arm.color = CosmeticModels.SuitColors[Suit];

            if (_hat != null) Destroy(_hat);
            _hat = CosmeticModels.Hat(Hat, _hatRoot, accessoryMaterial, CosmeticModels.HiddenLayer);
            if (_visor != null) Destroy(_visor);
            _visor = CosmeticModels.Visor(Visor, _visorRoot, visorMaterials, CosmeticModels.HiddenLayer);
            if (_badge != null) Destroy(_badge);
            _badge = CosmeticModels.Badge(Badge, _badgeAnchor, accessoryMaterial, 0);

            Changed?.Invoke();
        }
    }
}
