using UnityEngine;

namespace Monk.Cosmetics
{
    /// <summary>
    /// A real-time planar mirror. A hidden camera sits at your reflected eye position, behind the glass, and renders
    /// the room through the mirror's rectangle with an off-axis frustum, so the picture lines up exactly with the quad.
    /// It only renders while you are near and in front of it. Put it on a Quad that faces out of the mirror.
    /// </summary>
    public class MirrorSurface : MonoBehaviour
    {
        public const int MirrorLayer = 29;

        public Renderer surface;
        public Material mirrorMaterial;
        public int textureHeight = 768;
        public float activeDistance = 9f;
        public Color background = new Color(0.1f, 0.1f, 0.12f);

        private Camera _cam;
        private RenderTexture _rt;
        private Camera _viewer;

        private void Start()
        {
            float aspect = Mathf.Abs(transform.lossyScale.x / transform.lossyScale.y);
            _rt = new RenderTexture(Mathf.Max(64, Mathf.RoundToInt(textureHeight * aspect)), textureHeight, 16)
            {
                name = "MirrorRT", antiAliasing = 1,
            };

            var go = new GameObject("MirrorCamera");
            _cam = go.AddComponent<Camera>();
            _cam.targetTexture = _rt;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = background;
            _cam.cullingMask = ~(1 << MirrorLayer);
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.enabled = false;

            var mat = new Material(mirrorMaterial) { mainTexture = _rt };
            // The picture is rendered from behind the glass, so it has to be flipped horizontally on the front.
            mat.mainTextureScale = new Vector2(-1f, 1f);
            mat.mainTextureOffset = new Vector2(1f, 0f);
            surface.sharedMaterial = mat;
            surface.gameObject.layer = MirrorLayer;
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            if (_viewer == null) _viewer = Camera.main;
            if (_viewer == null) { _cam.enabled = false; return; }

            Vector3 c = transform.position;
            Vector3 n = -transform.forward;     // a Unity Quad faces its local -Z
            Vector3 vu = transform.up;
            Vector3 eye = _viewer.transform.position;
            float d = Vector3.Dot(eye - c, n);
            if (d < 0.05f || (eye - c).sqrMagnitude > activeDistance * activeDistance) { _cam.enabled = false; return; }

            float w = Mathf.Abs(transform.lossyScale.x), h = Mathf.Abs(transform.lossyScale.y);
            Vector3 eyeBehind = eye - 2f * d * n;               // the eye reflected across the mirror plane
            Vector3 vf = n;                                      // the hidden camera looks out of the mirror
            Vector3 vr = Vector3.Cross(vu, vf);                  // its right-hand direction

            Vector3 pa = c - vr * (w * 0.5f) - vu * (h * 0.5f);
            Vector3 pb = c + vr * (w * 0.5f) - vu * (h * 0.5f);
            Vector3 pc = c - vr * (w * 0.5f) + vu * (h * 0.5f);
            float l = Vector3.Dot(vr, pa - eyeBehind);
            float r = Vector3.Dot(vr, pb - eyeBehind);
            float b = Vector3.Dot(vu, pa - eyeBehind);
            float t = Vector3.Dot(vu, pc - eyeBehind);

            _cam.transform.SetPositionAndRotation(eyeBehind, Quaternion.LookRotation(vf, vu));
            _cam.nearClipPlane = d;                              // nothing behind the glass is drawn
            _cam.farClipPlane = 60f;
            _cam.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, d, 60f);
            _cam.enabled = true;
        }
    }
}
