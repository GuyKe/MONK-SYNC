using UnityEngine;

namespace Monk.Hub
{
    /// <summary>A security camera out in the Backrooms and the hub screen that shows what it sees.</summary>
    public class CctvCamera : MonoBehaviour
    {
        public Camera cam;
        public Renderer screen;
        public Material screenMaterial;
        public Light assistLight;
        public TextMesh label;
        public string title = "CAM";
        public int width = 320, height = 180;

        private RenderTexture _rt;

        private void Awake()
        {
            _rt = new RenderTexture(width, height, 16) { name = title };
            cam.targetTexture = _rt;
            cam.enabled = false;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.farClipPlane = 60f;
            screen.sharedMaterial = new Material(screenMaterial) { mainTexture = _rt };
            if (label != null) label.text = title;
            if (assistLight != null) assistLight.enabled = false;
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }

        public void SetRendering(bool on)
        {
            if (cam != null) cam.enabled = on;
            if (assistLight != null) assistLight.enabled = on;
        }
    }
}
