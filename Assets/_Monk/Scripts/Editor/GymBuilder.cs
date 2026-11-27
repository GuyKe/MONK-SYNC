using Monk.Locomotion;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace Monk.EditorTools
{
    /// <summary>Menu: Monk > Build M1 Locomotion Gym. Creates the player rig and a whitebox climbing gym in the open scene.</summary>
    public static class GymBuilder
    {
        private const int IgnoreRaycastLayer = 2;

        [MenuItem("Monk/Build M1 Locomotion Gym")]
        public static void Build()
        {
            var root = new GameObject("M1 Gym");
            Undo.RegisterCreatedObjectUndo(root, "Build M1 Gym");

            BuildGym(root.transform);
            BuildPlayer(root.transform);
            Selection.activeGameObject = root;
        }

        private static void BuildPlayer(Transform parent)
        {
            var profile = LoadOrCreateProfile();

            var rig = new GameObject("PlayerRig");
            rig.transform.SetParent(parent, false);
            rig.transform.position = new Vector3(0, 0.1f, 0);

            var head = new GameObject("Head");
            head.transform.SetParent(rig.transform, false);
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            head.tag = "MainCamera";
            head.AddComponent<AudioListener>();
            head.AddComponent<XRNodePoseDriver>().node = XRNode.CenterEye;

            var left = MakeHand("LeftHand", rig.transform, XRNode.LeftHand, profile.handRadius);
            var right = MakeHand("RightHand", rig.transform, XRNode.RightHand, profile.handRadius);
            var leftVis = MakeVisual("LeftHandVisual", parent, profile.handRadius);
            var rightVis = MakeVisual("RightHandVisual", parent, profile.handRadius);

            var body = new GameObject("BodyCollider") { layer = IgnoreRaycastLayer };
            body.transform.SetParent(rig.transform, false);
            var sphere = body.AddComponent<SphereCollider>();
            sphere.radius = profile.bodyRadius;

            var loco = rig.AddComponent<ArmLocomotion>();
            loco.profile = profile;
            loco.head = head.transform;
            loco.leftController = left;
            loco.rightController = right;
            loco.leftVisual = leftVis;
            loco.rightVisual = rightVis;
            loco.bodyCollider = sphere;
        }

        private static Transform MakeHand(string name, Transform parent, XRNode node, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<XRNodePoseDriver>().node = node;
            return go.transform;
        }

        private static Transform MakeVisual(string name, Transform parent, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.layer = IgnoreRaycastLayer;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * radius * 2f;
            return go.transform;
        }

        private static LocomotionProfile LoadOrCreateProfile()
        {
            const string path = "Assets/_Monk/LocomotionProfile.asset";
            var p = AssetDatabase.LoadAssetAtPath<LocomotionProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<LocomotionProfile>();
                AssetDatabase.CreateAsset(p, path);
                AssetDatabase.SaveAssets();
            }
            return p;
        }

        // ------------------------------------------------------------------ gym

        private static void BuildGym(Transform parent)
        {
            // Backrooms-ish proportions: 2.6 m ceiling hall, then a tall atrium to climb.
            Box(parent, "Floor", new Vector3(0, -0.5f, 10), new Vector3(30, 1, 40));
            Box(parent, "Ceiling Low", new Vector3(0, 2.8f, 0), new Vector3(10, 0.4f, 12));
            Box(parent, "Wall W", new Vector3(-5.2f, 4, 10), new Vector3(0.4f, 9, 40));
            Box(parent, "Wall E", new Vector3(5.2f, 4, 10), new Vector3(0.4f, 9, 40));
            Box(parent, "Wall S", new Vector3(0, 4, -6.2f), new Vector3(10.8f, 9, 0.4f));
            Box(parent, "Wall N", new Vector3(0, 4, 30.2f), new Vector3(10.8f, 9, 0.4f));
            Box(parent, "Ceiling High", new Vector3(0, 8.7f, 18), new Vector3(10, 0.4f, 24));

            // Atrium climbing features (z 6..30, 8 m tall).
            for (int i = 0; i < 7; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Box(parent, $"Ledge {i}", new Vector3(side * 3.6f, 1.2f + i * 1.1f, 9 + i * 2.5f), new Vector3(2.4f, 0.25f, 1.6f));
            }
            for (int i = 0; i < 4; i++)
                Box(parent, $"Pillar {i}", new Vector3(-2 + i * 1.4f, 3, 22), new Vector3(0.5f, 6, 0.5f));

            // Ceiling-push beam, and a pit to fall in.
            Box(parent, "Beam", new Vector3(0, 6.2f, 14), new Vector3(8, 0.3f, 0.5f));
            var slick = Box(parent, "Slippery Panel", new Vector3(0, 3, 29.8f), new Vector3(6, 4, 0.2f));
            slick.AddComponent<SurfaceProperties>().grip = 0f;
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            return go;
        }
    }
}
