using UnityEngine;

namespace EcclesiaFighters
{
    /// <summary>
    /// Handles combat projectile damage, world-space camera-facing spin & travel orientation,
    /// and trigger collision for fighter special projectiles.
    /// </summary>
    public class ProjectileDamage : MonoBehaviour
    {
        [Header("Combat & Spin")]
        public float damage    = 38f;
        public bool  spinSelf  = false;
        public float spinSpeed = 420f;

        [Header("3D Visual Orientation")]
        public Transform  visualRoot;
        public Quaternion baseVisualRotation       = Quaternion.identity;
        public bool       hasCustomBaseRotation    = false;
        public float      tiltTowardsTravelDegrees = 14f;

        [HideInInspector] public GameObject owner;
        [HideInInspector] public int        travelSign = 1;

        float spinAngle;

        void Start()
        {
            if (visualRoot == null)
            {
                Transform v = transform.Find("Visual");
                if (v != null) visualRoot = v;
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null && Mathf.Abs(rb.velocity.x) > 0.05f)
                travelSign = rb.velocity.x >= 0f ? 1 : -1;
            else if (Mathf.Abs(transform.forward.x) > 0.05f)
                travelSign = transform.forward.x >= 0f ? 1 : -1;

            ApplyVisualOrientation();
        }

        public void SetTravelDirection(Vector3 dir)
        {
            if (Mathf.Abs(dir.x) > 0.01f)
                travelSign = dir.x >= 0f ? 1 : -1;
            ApplyVisualOrientation();
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (!spinSelf) return;

            spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.deltaTime, 360f);
            if (visualRoot != null && visualRoot != transform)
                ApplyVisualOrientation();
            else
                transform.Rotate(0f, 0f, -travelSign * spinSpeed * Time.deltaTime, Space.World);
        }

        void ApplyVisualOrientation()
        {
            if (visualRoot == null || visualRoot == transform) return;
            Quaternion baseRot = hasCustomBaseRotation ? baseVisualRotation : Quaternion.identity;
            Quaternion tiltRot = Quaternion.Euler(0f, -travelSign * tiltTowardsTravelDegrees, 0f);
            Quaternion spinRot = Quaternion.Euler(0f, 0f, -travelSign * spinAngle);
            visualRoot.rotation = tiltRot * spinRot * baseRot;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == owner) return;
            var f = other.GetComponentInParent<FighterController>();
            if (f != null && f.gameObject != owner)
            {
                Vector3 kbDir = f.transform.position.x >= transform.position.x ? Vector3.right : Vector3.left;
                f.TakeDamage(damage, kbDir, 4.0f, 0.24f, isHeavyHit: true, isSpecialProjectile: true);
                Destroy(gameObject);
                return;
            }
            if (other.CompareTag("Ground") || other.CompareTag("Wall"))
                Destroy(gameObject);
        }
    }
}
