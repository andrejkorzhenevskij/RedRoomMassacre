using UnityEngine;
using UnityEngine.SceneManagement;

namespace RRM
{
    public sealed partial class Damageable
    {
        private int severedLimbs;
        public LimbState GetLimbState(BodyPart part) => IsAttached(part) ? LimbState.Attached : LimbState.Severed;
        public bool IsAttached(BodyPart part) => (severedLimbs & (1 << (int)part)) == 0;
        public bool HasBothLegs => IsAttached(BodyPart.LeftLeg) && IsAttached(BodyPart.RightLeg);
        public int SeveredLimbCount
        {
            get
            {
                int count = 0;
                for (int bits = severedLimbs; bits != 0; bits &= bits - 1) count++;
                return count;
            }
        }

        private void DetachLimb(Hurtbox zone, Vector3 direction)
        {
            // Copy only the blockout mesh, never the actor/hand hierarchy or its scripts.
            bool visualProxy = TryGetComponent(out CharCrafterVisual rig);
            var loose = new GameObject((visualProxy ? "Severed Proxy " : "Severed ") + zone.part);
            SceneManager.MoveGameObjectToScene(loose, gameObject.scene);
            loose.transform.SetPositionAndRotation(zone.transform.position, zone.transform.rotation);
            loose.transform.localScale = zone.transform.lossyScale;
            loose.AddComponent<MeshFilter>().sharedMesh = zone.GetComponent<MeshFilter>().sharedMesh;
            loose.AddComponent<MeshRenderer>().sharedMaterials = zone.GetComponent<MeshRenderer>().sharedMaterials;
            var shape = loose.AddComponent<BoxCollider>();
            var body = loose.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = (GetComponent<Rigidbody>()?.linearVelocity ?? Vector3.zero) + direction.normalized * 0.5f;
            HandItem.IgnoreActors(shape);
            loose.layer = LayerMask.NameToLayer("RRMActors");

            GetComponent<PlayerController>()?.OnLimbSevered(zone.part);
            zone.GetComponent<Renderer>().enabled = false;
            zone.GetComponent<Collider>().enabled = false;
            zone.enabled = false;

            // CharCrafter's body/clothing are whole meshes. Keep the skeleton intact; this is only a detached proxy.
            if (visualProxy) return;

            // ponytail: closed cube parts and a small cap suit this blockout; no mesh slicing or bone removal.
            bool arm = zone.part == BodyPart.LeftArm || zone.part == BodyPart.RightArm;
            bool left = zone.part == BodyPart.LeftArm || zone.part == BodyPart.LeftLeg;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = zone.part + " Stump";
            cap.GetComponent<Collider>().enabled = false;
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(GetComponent<HitFeedback>()?.visual ?? transform, false);
            cap.transform.localPosition = new Vector3((left ? -1f : 1f) * (arm ? 0.32f : 0.18f), arm ? 1.2f : 0.73f, 0);
            cap.transform.localScale = arm ? new Vector3(0.06f, 0.2f, 0.22f) : new Vector3(0.23f, 0.06f, 0.28f);
            cap.GetComponent<Renderer>().sharedMaterial = GetComponent<BloodEvidence>()?.bloodMaterial
                ?? zone.GetComponent<Renderer>().sharedMaterial;
        }
    }
}
