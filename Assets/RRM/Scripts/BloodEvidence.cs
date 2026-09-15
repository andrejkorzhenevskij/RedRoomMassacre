using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RRM
{
    public readonly struct BloodEvent
    {
        public readonly Vector3 Position;
        public readonly GameObject Mark;

        public BloodEvent(Vector3 position, GameObject mark)
        {
            Position = position;
            Mark = mark;
        }
    }

    [RequireComponent(typeof(Damageable)), DisallowMultipleComponent]
    public sealed class BloodEvidence : MonoBehaviour
    {
        public Material bloodMaterial;
        public LayerMask surfaceMask = 1;
        [Min(0.01f)] public float wallReach = 0.85f;
        [Min(0.01f)] public float floorReach = 2f;
        [Min(0.05f)] public float trailSpacing = 0.55f;
        public Vector2 trailSize = new Vector2(0.24f, 0.18f);
        [Min(1)] public int maxMarks = 128;
        public IReadOnlyList<BloodEvent> Marks => marks;
        public event Action<BloodEvent> Created;

        private readonly List<BloodEvent> marks = new List<BloodEvent>();
        private Damageable health;
        private CapsuleCollider capsule;
        private Vector3 previousTrailPoint;
        private float trailDistance;
        private bool hasTrailPoint;
        private static readonly Vector3[] WallDirections =
            { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        private void Awake()
        {
            health = GetComponent<Damageable>();
            capsule = GetComponent<CapsuleCollider>();
        }
        private void OnEnable() => health.Damaged += LeaveMark;
        private void OnDisable() { health.Damaged -= LeaveMark; hasTrailPoint = false; trailDistance = 0f; }

        private void LateUpdate()
        {
            if (!bloodMaterial || !(health.BleedingPerSecond > 0d) || !capsule
                || !Physics.Raycast(new Vector3(transform.position.x, capsule.bounds.min.y + 0.06f, transform.position.z),
                    Vector3.down, out RaycastHit surface, 0.12f, surfaceMask, QueryTriggerInteraction.Ignore)
                || surface.normal.y < 0.5f)
            {
                hasTrailPoint = false; trailDistance = 0f;
                return;
            }
            if (!hasTrailPoint) { previousTrailPoint = surface.point; hasTrailPoint = true; return; }
            float travelled = Vector3.Distance(previousTrailPoint, surface.point);
            if (travelled < 0.005f) return;
            previousTrailPoint = surface.point;
            float spacing = Mathf.Max(0.05f, trailSpacing);
            // ponytail: at most one mark per rendered frame, no interpolation across gaps or teleports.
            if (travelled > Mathf.Max(2f, spacing * 4f)) { trailDistance = 0f; return; }
            trailDistance += travelled;
            if (trailDistance < spacing) return;
            trailDistance %= spacing;
            PlaceMark(surface, trailSize);
        }

        private void LeaveMark(DamageEvent hit)
        {
            if (!bloodMaterial || hit.IsBleeding) return;
            bool found = false;
            RaycastHit surface = default;
            float nearest = wallReach;
            // Nearby walls take priority; otherwise project the impact onto the floor below.
            foreach (Vector3 direction in WallDirections)
                if (Physics.Raycast(hit.Point, direction, out RaycastHit wall, nearest,
                        surfaceMask, QueryTriggerInteraction.Ignore))
                {
                    surface = wall;
                    nearest = wall.distance;
                    found = true;
                }
            if (!found && !Physics.Raycast(hit.Point, Vector3.down, out surface, floorReach,
                    surfaceMask, QueryTriggerInteraction.Ignore)) return;

            PlaceMark(surface, new Vector2(0.45f, 0.32f));
        }

        private void PlaceMark(RaycastHit surface, Vector2 size)
        {
            while (marks.Count >= Mathf.Max(1, maxMarks))
            {
                if (marks[0].Mark) Destroy(marks[0].Mark);
                marks.RemoveAt(0);
            }
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = "Blood Evidence";
            Collider collider = mark.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            SceneManager.MoveGameObjectToScene(mark, gameObject.scene);
            Vector3 position = surface.point + surface.normal * 0.015f;
            mark.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-surface.normal,
                Mathf.Abs(surface.normal.y) > 0.9f ? Vector3.forward : Vector3.up));
            mark.transform.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
            Renderer renderer = mark.GetComponent<Renderer>();
            renderer.sharedMaterial = bloodMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var evidence = new BloodEvent(position, mark);
            marks.Add(evidence);
            Created?.Invoke(evidence);
        }
    }
}
