using UnityEngine;
using UnityEngine.XR;

namespace Monk.Locomotion
{
    /// <summary>
    /// Minimal tracked-pose driver using the core XR API (no extra packages).
    /// Sets this transform's local pose from an XR node, relative to its parent (tracking space).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class XRNodePoseDriver : MonoBehaviour
    {
        public XRNode node = XRNode.Head;
        [Tooltip("Local offset in the device's frame, e.g. to move from grip origin to palm.")]
        public Vector3 positionOffset;

        private InputDevice _device;

        private void OnEnable() => Application.onBeforeRender += Apply;
        private void OnDisable() => Application.onBeforeRender -= Apply;
        private void Update() => Apply();

        private void Apply()
        {
            if (!_device.isValid) _device = InputDevices.GetDeviceAtXRNode(node);
            if (!_device.isValid) return;

            if (_device.TryGetFeatureValue(CommonUsages.devicePosition, out var p) &&
                _device.TryGetFeatureValue(CommonUsages.deviceRotation, out var r))
            {
                transform.localRotation = r;
                transform.localPosition = p + r * positionOffset;
            }
        }
    }
}
