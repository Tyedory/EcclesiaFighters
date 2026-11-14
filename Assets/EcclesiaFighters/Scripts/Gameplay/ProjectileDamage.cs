using UnityEngine;

namespace EcclesiaFighters
{
    /// <summary>
    /// Attach to projectile prefabs (foice, martelo, raio de luz).
    /// Set spinSelf = true for Marx's sickle/hammer.
    /// </summary>
    public class ProjectileDamage : MonoBehaviour
    {
        public float  damage    = 38f;
        public bool   spinSelf  = false;
        public float  spinSpeed = 420f;
        [HideInInspector] public GameObject owner;

        void Update()
        {
            if (spinSelf) transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject == owner) return;
            var f = other.GetComponent<FighterController>();
            if (f != null) { f.TakeDamage(damage); Destroy(gameObject); return; }
            if (other.CompareTag("Ground") || other.CompareTag("Wall"))
                Destroy(gameObject);
        }
    }
}
