using System.Collections.Generic;
using UnityEngine;

namespace Atthree.Combat
{
    /// <summary>
    /// Hasar yardımcıları: raycast/çarpışmadan gelen bir collider'a ya da alana hasar ver.
    ///
    ///   if (Physics.Raycast(ray, out var hit)) Damage.Apply(hit.collider, new DamageInfo(10) { point = hit.point });
    ///   Damage.Explosion(transform.position, 5f, new DamageInfo(80, fire, gameObject), force: 15f);
    /// </summary>
    public static class Damage
    {
        static readonly Collider[] overlap = new Collider[256];
        static readonly Dictionary<Health, (Collider col, float dist)> closest = new Dictionary<Health, (Collider, float)>();
        static readonly HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();

        /// <summary>Collider'daki IDamageable'ı bul: önce kendisi (Hitbox), sonra üst objeler (Health).</summary>
        public static IDamageable Find(Component target)
        {
            if (target == null) return null;
            if (target.TryGetComponent<IDamageable>(out var d)) return d;
            return target.GetComponentInParent<IDamageable>();
        }

        /// <summary>Hedefe hasar ver. Hedef hasar alamıyorsa false.</summary>
        public static bool Apply(Component target, DamageInfo info) => Apply(target, info, out _);

        public static bool Apply(Component target, DamageInfo info, out DamageResult result)
        {
            var d = Find(target);
            if (d == null) { result = default; return false; }
            result = d.TakeDamage(info);
            return true;
        }

        public static bool Apply(GameObject target, DamageInfo info) => target != null && Apply(target.transform, info);

        /// <summary>
        /// Alan hasarı. Her Health bir kez vurulur (en yakın collider'ı üzerinden, Hitbox çarpanıyla).
        /// <paramref name="falloff"/> 1 = kenarda hasar 0, 0 = her yerde tam hasar.
        /// Engel kontrolü yok: duvar arkasını korumak istersen <paramref name="obstacleMask"/> ver.
        /// Vurulan Health sayısını döndürür.
        /// </summary>
        public static int Explosion(Vector3 center, float radius, DamageInfo info,
            LayerMask? mask = null, float falloff = 1f, float force = 0f, float upwardsModifier = 0.5f,
            LayerMask? obstacleMask = null, List<DamageResult> results = null)
        {
            int layer = mask ?? Physics.AllLayers;
            int n = Physics.OverlapSphereNonAlloc(center, radius, overlap, layer, QueryTriggerInteraction.Ignore);

            closest.Clear();
            pushed.Clear();
            var loose = new List<(IDamageable d, Collider col, float dist)>(); // Health'e bağlı olmayan IDamageable'lar

            for (int i = 0; i < n; i++)
            {
                var col = overlap[i];
                var p = col.ClosestPoint(center);
                float dist = Vector3.Distance(center, p);

                if (force > 0f && col.attachedRigidbody != null && pushed.Add(col.attachedRigidbody))
                    col.attachedRigidbody.AddExplosionForce(force, center, radius, upwardsModifier, ForceMode.Impulse);

                var d = Find(col);
                if (d == null) continue;
                if (obstacleMask.HasValue && Blocked(center, p, col, obstacleMask.Value)) continue;

                var h = d as Health ?? (d as Hitbox)?.Health;
                if (h == null) { loose.Add((d, col, dist)); continue; }
                if (!closest.TryGetValue(h, out var best) || dist < best.dist) closest[h] = (col, dist);
            }

            int hits = 0;
            foreach (var kv in closest)
            {
                if (Hit(Find(kv.Value.col), kv.Value.col, kv.Value.dist, center, radius, info, falloff, results)) hits++;
            }
            foreach (var (d, col, dist) in loose)
                if (Hit(d, col, dist, center, radius, info, falloff, results)) hits++;
            closest.Clear();
            return hits;
        }

        static bool Hit(IDamageable d, Collider col, float dist, Vector3 center, float radius, DamageInfo info, float falloff, List<DamageResult> results)
        {
            float t = radius > 0f ? Mathf.Clamp01(dist / radius) : 0f;
            var hit = info;
            hit.amount = info.amount * (1f - t * Mathf.Clamp01(falloff));
            if (hit.amount <= 0f) return false;
            var target = col.bounds.center;
            hit.point = col.ClosestPoint(center);
            hit.direction = (target - center).sqrMagnitude > 1e-6f ? (target - center).normalized : Vector3.up;
            var r = d.TakeDamage(hit);
            results?.Add(r);
            return true;
        }

        static bool Blocked(Vector3 from, Vector3 to, Collider target, LayerMask obstacles)
        {
            var dir = to - from;
            float len = dir.magnitude;
            if (len < 1e-4f) return false;
            return Physics.Raycast(from, dir / len, out var hit, len, obstacles, QueryTriggerInteraction.Ignore)
                   && hit.collider != target;
        }
    }
}
