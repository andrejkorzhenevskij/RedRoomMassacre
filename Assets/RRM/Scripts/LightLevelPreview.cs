using UnityEngine;
using UnityEngine.Rendering;

namespace RRM
{
    [RequireComponent(typeof(MeshRenderer)), DisallowMultipleComponent]
    public sealed class LightLevelPreview : MonoBehaviour
    {
        public Material unlitTemplate;
        [Range(0f, 0.3f)] public float opacity = 0.18f;

        private GameObject overlay;
        private Material material;
        private Texture2D texture;
        private Color[] pixels;

        private void OnEnable()
        {
            if (!unlitTemplate) return;
            Bounds floor = GetComponent<MeshRenderer>().bounds;
            overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "Light Level Preview";
            Collider collider = overlay.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            // ponytail: one flat floor; sample raised surfaces separately when they need a preview.
            overlay.transform.SetPositionAndRotation(new Vector3(floor.center.x, floor.max.y + 0.005f, floor.center.z),
                Quaternion.Euler(90, 0, 0));
            overlay.transform.localScale = new Vector3(floor.size.x, floor.size.z, 1);
            overlay.transform.SetParent(transform, true);

            texture = new Texture2D(128, 96, TextureFormat.RGBA32, false, true)
            {
                name = "Light Level Preview", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color[texture.width * texture.height];
            material = new Material(unlitTemplate) { name = "Light Level Preview", renderQueue = (int)RenderQueue.Transparent };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            // The saved transparent template keeps this shader variant in standalone builds.
            var renderer = overlay.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Refresh();
            InvokeRepeating(nameof(Refresh), 0.2f, 0.2f);
        }

        private void Refresh()
        {
            LightSource[] sources = FindObjectsByType<LightSource>();
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    Vector3 point = overlay.transform.TransformPoint(new Vector3(
                        (x + 0.5f) / texture.width - 0.5f, (y + 0.5f) / texture.height - 0.5f, 0));
                    // Clear bright areas; keep the same maximum tint and leave real shadows alone.
                    float alpha = Mathf.Clamp(opacity, 0f, 0.3f)
                        * (1f - Mathf.SmoothStep(0f, 1f, LightSource.At(point, sources) / 70f));
                    pixels[y * texture.width + x] = new Color(0.55f, 0.3f, 0.3f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false);
        }

        private void OnDisable()
        {
            CancelInvoke();
            if (overlay) Destroy(overlay);
            if (material) Destroy(material);
            if (texture) Destroy(texture);
        }
    }
}
