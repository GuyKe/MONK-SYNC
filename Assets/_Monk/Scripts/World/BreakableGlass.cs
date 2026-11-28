using Monk.Audio;
using Monk.Gameplay;
using Monk.Locomotion;
using UnityEngine;

namespace Monk.World
{
    /// <summary>
    /// A clear glass pane. A hand hitting it fast enough shatters it into physical shards and leaves a gap you can
    /// climb through. Slower touches just rest on it like any other surface.
    /// </summary>
    public class BreakableGlass : MonoBehaviour, IHandPressable
    {
        public Renderer rend;
        public Collider col;
        public Material shardMaterial;
        [Tooltip("Hand speed (m/s) needed to break it.")]
        public float breakSpeed = 3f;
        public int shardCount = 22;
        public float shardLifetime = 8f;

        public bool Broken { get; private set; }

        public void Press(HandContactEvent contact)
        {
            if (Broken || contact.speed < breakSpeed) return;
            Break(contact.point, contact.normal);
        }

        public void Break(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (Broken) return;
            Broken = true;
            if (rend != null) rend.enabled = false;
            if (col != null) col.enabled = false;

            // The pane is thin along its local X; Y is height and Z is width.
            Vector3 size = col is BoxCollider box ? box.size : new Vector3(0.05f, 1.5f, 1.5f);
            for (int i = 0; i < shardCount; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "GlassShard";
                shard.layer = 2; // physical, but hands ignore it
                shard.transform.position = transform.TransformPoint(new Vector3(0f,
                    (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z));
                shard.transform.rotation = transform.rotation * Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(-30f, 30f), Random.Range(-30f, 30f));
                shard.transform.localScale = new Vector3(0.012f, Random.Range(0.1f, 0.4f), Random.Range(0.1f, 0.35f));
                if (shardMaterial != null) shard.GetComponent<MeshRenderer>().sharedMaterial = shardMaterial;

                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                Vector3 away = -hitNormal;
                rb.linearVelocity = away * Random.Range(0.5f, 2.5f) + Random.insideUnitSphere * 1.2f;
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                Destroy(shard, shardLifetime + Random.value * 3f);
            }

            AudioSource.PlayClipAtPoint(ProceduralAudio.Shatter(), transform.position, 1f);
            NoiseBus.Emit(transform.position, 1f);
        }
    }
}
