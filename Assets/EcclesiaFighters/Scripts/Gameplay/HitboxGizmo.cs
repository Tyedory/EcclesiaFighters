using UnityEngine;
// Optional helper — attach to a child GameObject "Hitbox" on your prefab.
// Visualizes the attack range in Scene view.
namespace EcclesiaFighters
{
    public class HitboxGizmo : MonoBehaviour
    {
        public float radius = 0.75f;
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1,0,0,0.4f);
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
