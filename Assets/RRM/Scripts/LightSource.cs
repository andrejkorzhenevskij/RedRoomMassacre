using UnityEngine;

namespace RRM
{
    public enum VisibilityLevel { Normal, Low, Dark }

    [DisallowMultipleComponent]
    public sealed class LightSource : MonoBehaviour
    {
        [Range(0f, 100f)] public float intensity = 100f;
        [Min(0.01f)] public float radius = 5f;
        [Tooltip("Optional real lamp. Intensity and range are derived from this LightLevel source.")]
        public Light pointLight;

        private void OnEnable() => SyncPointLight();
        private void LateUpdate() => SyncPointLight();
        private void OnValidate() => SyncPointLight();
        private void OnDisable() { if (pointLight) pointLight.enabled = false; }

        public void SyncPointLight()
        {
            if (!pointLight) return;
            pointLight.type = LightType.Point;
            pointLight.intensity = float.IsFinite(intensity) ? Mathf.Clamp01(intensity / 100f) * 2f : 0f;
            pointLight.range = float.IsFinite(radius) ? Mathf.Max(0.01f, radius) : 0.01f;
            pointLight.enabled = isActiveAndEnabled && intensity > 0f && radius > 0f;
        }

        public float ContributionAt(Vector3 point)
        {
            if (!isActiveAndEnabled || !(radius > 0f) || float.IsInfinity(radius)
                || !(intensity > 0f) || float.IsInfinity(intensity)) return 0f;
            float distance = Vector3.Distance(transform.position, point);
            if (!(distance < radius)) return 0f;
            return Mathf.Clamp(intensity, 0f, 100f) * (1f - Mathf.SmoothStep(0f, 1f, distance / radius));
        }

        public static float At(Vector3 point) => At(point, FindObjectsByType<LightSource>());

        internal static float At(Vector3 point, LightSource[] sources)
        {
            float total = 0f;
            // ponytail: a few logical sources, no shadow rays; add spatial lookup/occlusion when needed.
            foreach (LightSource source in sources) total += source.ContributionAt(point);
            return Mathf.Clamp(total, 0f, 100f);
        }

        public static VisibilityLevel Classify(float lightLevel) =>
            lightLevel >= 65f ? VisibilityLevel.Normal : lightLevel >= 25f ? VisibilityLevel.Low : VisibilityLevel.Dark;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, radius));
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, radius) * 0.5f);
        }
    }
}
