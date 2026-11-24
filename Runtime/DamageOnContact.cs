using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Atthree.Combat
{
    /// <summary>
    /// Dokunana hasar verir: diken, lav, mermi, kılıç kutusu, düşme/çarpma hasarı.
    /// Hem Collision hem Trigger ile çalışır (collider'ın "Is Trigger" ayarına göre).
    /// Fizik mesajları için iki taraftan birinde Rigidbody olmalı.
    /// </summary>
    [AddComponentMenu("Atthree/Combat/Damage On Contact")]
    public class DamageOnContact : MonoBehaviour
    {
        [Header("Hasar")]
        [Min(0f)] public float damage = 10f;
        public DamageType damageType;
        [Tooltip("Hangi katmanlara vurur?")]
        public LayerMask hitLayers = ~0;
        [Tooltip("Hasarı verenin takımı. Aynı takımdaki Health'ler (dost ateşi kapalıysa) etkilenmez. -1 = takımsız.")]
        public int team = DamageInfo.NoTeam;
        [Tooltip("Hasar kaynağı olarak gösterilecek obje (puan, öldüren). Boşsa bu obje.")]
        public GameObject source;

        [Header("Temas")]
        [Tooltip("İçinde kalındıkça bu aralıkla tekrar vurur (lav, zehir bulutu). 0 = sadece ilk temasta.")]
        [Min(0f)] public float repeatInterval;
        [Tooltip("Kendi hiyerarşindeki Health'e vurma.")]
        public bool ignoreOwnHierarchy = true;
        [Tooltip("İlk vuruştan sonra bu objeyi yok et (mermi).")]
        public bool destroyOnHit;
        [Tooltip("Hasar alamayan bir şeye (duvar) çarpınca da yok et.")]
        public bool destroyOnAnyContact;

        [Header("Çarpma hızına göre (çarpışma)")]
        [Tooltip("Hasar çarpma hızıyla ölçeklensin (düşen kaya, araba). Sadece Collision'da çalışır.")]
        public bool scaleWithImpactSpeed;
        [Tooltip("Bu hızın altındaki çarpmalar hasar vermez.")]
        [Min(0f)] public float minImpactSpeed = 3f;
        [Tooltip("Hız başına hasar (min hızın üstündeki her 1 m/s için).")]
        [Min(0f)] public float damagePerSpeed = 5f;

        [Header("Geri itme")]
        [Tooltip("Vurulanın Rigidbody'sine verilecek itme (Impulse).")]
        [Min(0f)] public float knockback;

        [Header("Olaylar")]
        public UnityEvent<GameObject> onHit = new UnityEvent<GameObject>();

        readonly Dictionary<IDamageable, float> lastHit = new Dictionary<IDamageable, float>();
        readonly List<IDamageable> stale = new List<IDamageable>();
        bool spent;

        void OnEnable()
        {
            spent = false;
            lastHit.Clear();
        }

        void OnCollisionEnter(Collision c) => Handle(c.collider, ContactPoint(c), c.relativeVelocity.magnitude, true);
        void OnCollisionStay(Collision c) { if (repeatInterval > 0f) Handle(c.collider, ContactPoint(c), 0f, false); }

        static Vector3 ContactPoint(Collision c) => c.contactCount > 0 ? c.GetContact(0).point : c.collider.bounds.center;
        void OnTriggerEnter(Collider other) => Handle(other, other.ClosestPoint(transform.position), 0f, true);
        void OnTriggerStay(Collider other) { if (repeatInterval > 0f) Handle(other, other.ClosestPoint(transform.position), 0f, false); }

        void Handle(Collider other, Vector3 point, float impactSpeed, bool isEnter)
        {
            if (spent || !isActiveAndEnabled) return;
            if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

            var target = Damage.Find(other);
            if (target == null)
            {
                if (isEnter && destroyOnAnyContact && !other.isTrigger) Spend();
                return;
            }
            if (ignoreOwnHierarchy && SameHierarchy(target)) return;

            float amount = damage;
            if (scaleWithImpactSpeed)
            {
                if (!isEnter) return; // hız sadece ilk temasta anlamlı
                if (impactSpeed < minImpactSpeed) return;
                amount = damage + (impactSpeed - minImpactSpeed) * damagePerSpeed;
            }

            // Aynı hedefe aralık dolmadan tekrar vurma (aynı Health'in birden çok collider'ı da tek sayılır).
            var key = (target as Hitbox)?.Health as IDamageable ?? target;
            if (lastHit.TryGetValue(key, out float t))
            {
                if (repeatInterval <= 0f || Time.time - t < repeatInterval) return;
            }
            else if (!isEnter && repeatInterval <= 0f) return;
            lastHit[key] = Time.time;
            CleanUp();

            var dir = other.bounds.center - transform.position;
            var info = new DamageInfo(amount, damageType, source ? source : gameObject)
            {
                point = point,
                direction = dir.sqrMagnitude > 1e-6f ? dir.normalized : transform.forward,
                team = team,
            };
            var result = target.TakeDamage(info);

            if (!result.WasBlocked && knockback > 0f && other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
                other.attachedRigidbody.AddForce(info.direction * knockback, ForceMode.Impulse);

            if (!result.WasBlocked) onHit.Invoke(other.gameObject);
            if (destroyOnHit && result.blocked != DamageBlock.FriendlyFire) Spend();
        }

        // Kılıç karakterin çocuğu, karakterin Health'i kökte: biri diğerinin atası ise "kendimiz" say.
        bool SameHierarchy(IDamageable target)
        {
            var t = ((target as Hitbox)?.Health as Component ?? target as Component)?.transform;
            if (t == null) return false;
            return transform.IsChildOf(t) || t.IsChildOf(transform);
        }

        void CleanUp()
        {
            if (lastHit.Count < 32) return;
            stale.Clear();
            foreach (var kv in lastHit)
                if (kv.Key is Object o && o == null) stale.Add(kv.Key);
            foreach (var k in stale) lastHit.Remove(k);
        }

        void Spend()
        {
            spent = true;
            Destroy(gameObject);
        }

        /// <summary>Hedef hafızasını sıfırla (havuzdan tekrar kullanılan mermi için).</summary>
        public void ResetHits()
        {
            lastHit.Clear();
            spent = false;
        }
    }
}
