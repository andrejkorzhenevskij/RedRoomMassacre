using UnityEngine;

namespace RRM
{
    [RequireComponent(typeof(Damageable), typeof(Rigidbody), typeof(AudioSource))]
    public sealed class HitFeedback : MonoBehaviour
    {
        public Transform visual;
        public Renderer[] bodyRenderers;
        public ParticleSystem blood;
        public AudioClip impactSound;
        [Min(0f)] public float recoilAngle = 12f;

        private Damageable health;
        private Rigidbody body;
        private AudioSource audioSource;
        private MaterialPropertyBlock properties;
        private float recoil;
        private float flash;
        private Vector3 recoilAxis;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            health = GetComponent<Damageable>();
            body = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable() { if (health) health.Damaged += React; }
        private void OnDisable() { if (health) health.Damaged -= React; }

        private void React(DamageEvent hit)
        {
            body.AddForce(hit.Direction * hit.Impulse, ForceMode.VelocityChange);
            recoil = recoilAngle;
            flash = 0.1f;
            recoilAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, hit.Direction));
            if (blood)
            {
                blood.transform.position = hit.Point;
                blood.transform.rotation = Quaternion.LookRotation(hit.Direction + Vector3.up * 0.5f);
                blood.Play();
                blood.Emit(hit.IsFatal ? 32 : 20);
            }
            if (impactSound)
            {
                audioSource.pitch = hit.IsFatal ? 0.75f : 0.95f;
                audioSource.PlayOneShot(impactSound, 0.7f);
            }
            if (hit.IsFatal)
            {
                body.constraints = RigidbodyConstraints.None;
                body.AddTorque(Vector3.Cross(Vector3.up, hit.Direction) * 3f, ForceMode.VelocityChange);
                foreach (Hurtbox zone in GetComponentsInChildren<Hurtbox>())
                    zone.GetComponent<Collider>().enabled = false;
            }
        }

        private void LateUpdate()
        {
            recoil = Mathf.MoveTowards(recoil, 0f, 65f * Time.deltaTime);
            flash = Mathf.Max(0f, flash - Time.deltaTime);
            if (visual) visual.localRotation = Quaternion.AngleAxis(recoil, recoilAxis);
            if (bodyRenderers == null) return;
            foreach (Renderer part in bodyRenderers)
            {
                if (!part) continue;
                properties.Clear();
                if (flash > 0f) properties.SetColor("_BaseColor", new Color(0.9f, 0.12f, 0.1f));
                else if (health.IsDead) properties.SetColor("_BaseColor", new Color(0.26f, 0.07f, 0.08f));
                part.SetPropertyBlock(properties);
            }
        }
    }
}
