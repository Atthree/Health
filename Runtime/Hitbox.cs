using UnityEngine;

namespace Atthree.Combat
{
    /// <summary>
    /// Bir collider'a "vurulabilir bölge" der ve hasarı üstteki Health'e iletir. Kafa = 2x + kritik, bacak = 0.75x gibi.
    /// Ragdoll/karakter kemiklerindeki collider'lara eklenir; Health kökteki objede durur.
    /// </summary>
    [AddComponentMenu("Atthree/Combat/Hitbox")]
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour, IDamageable
    {
        [Tooltip("Boşsa üst objelerdeki ilk Health bulunur.")]
        [SerializeField] Health health;

        [Tooltip("Bu bölgeye gelen hasarın çarpanı (kafa 2, gövde 1, bacak 0.75).")]
        [Min(0f)] public float multiplier = 1f;

        [Tooltip("Bu bölgeye vuruş kritik sayılsın (kafa).")]
        public bool critical;

        public Health Health
        {
            get
            {
                if (health == null) health = GetComponentInParent<Health>(true);
                return health;
            }
            set => health = value;
        }

        public DamageResult TakeDamage(DamageInfo info)
        {
            var h = Health;
            if (h == null) return default;
            info.amount *= multiplier;
            info.critical |= critical;
            return h.TakeDamage(info);
        }
    }
}
