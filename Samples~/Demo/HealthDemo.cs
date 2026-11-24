using System.Collections;
using UnityEngine;

namespace Atthree.Combat.Samples
{
    /// <summary>
    /// Kendini kuran demo. Boş bir sahnede boş bir objeye ekle ve Play'e bas:
    ///  • Ortadaki taret sırayla hedeflere mermi (DamageOnContact) atar.
    ///  • Hedefler: normal / zırhlı / ateşe dirençli / i-frame'li / kendini yenileyen. Hepsinde can barı var.
    ///  • Her 6 saniyede bir patlama (Damage.Explosion) olur; ölen hedefler 3 saniye sonra dirilir.
    ///  • Hitbox: "Kafa" küresine gelen vuruş 2x ve kritik.
    /// Console'da her hasar ve ölüm yazılır (Health.AnyDamaged / AnyDied).
    /// </summary>
    public class HealthDemo : MonoBehaviour
    {
        public float fireInterval = 0.6f;
        public float projectileSpeed = 18f;
        public float explosionInterval = 6f;

        DamageType fire;
        Health[] targets;
        Transform turret;

        void Start()
        {
            fire = ScriptableObject.CreateInstance<DamageType>();
            fire.name = "Fire";
            fire.color = new Color(1f, 0.5f, 0.1f);

            BuildScene();
            Health.AnyDamaged += LogDamage;
            Health.AnyDied += LogDeath;
            StartCoroutine(FireLoop());
            StartCoroutine(ExplosionLoop());
        }

        void OnDestroy()
        {
            Health.AnyDamaged -= LogDamage;
            Health.AnyDied -= LogDeath;
        }

        void BuildScene()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * 3f;
            Tint(ground, new Color(0.3f, 0.32f, 0.35f));

            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
            }
            Camera.main.transform.SetPositionAndRotation(new Vector3(0f, 11f, -13f), Quaternion.Euler(40f, 0f, 0f));

            if (FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            turret.name = "Turret";
            turret.position = new Vector3(0f, 0.5f, -3f);
            turret.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            Tint(turret.gameObject, Color.gray);

            targets = new[]
            {
                Target("Normal", new Vector3(-6f, 1f, 4f), Color.white, h => { }),
                Target("Armored (armor 5)", new Vector3(-3f, 1f, 5f), new Color(0.5f, 0.6f, 0.8f), h => h.armor = 5f),
                Target("Fire resistant (x0.25)", new Vector3(0f, 1f, 5.5f), new Color(1f, 0.6f, 0.3f),
                    h => h.resistances.Add(new Health.Resistance { type = fire, multiplier = 0.25f })),
                Target("I-frames (1s)", new Vector3(3f, 1f, 5f), new Color(0.8f, 0.5f, 1f), h => h.invulnerabilityAfterHit = 1f),
                Target("Regen (15/s)", new Vector3(6f, 1f, 4f), new Color(0.5f, 1f, 0.6f), h => { h.regenPerSecond = 15f; h.regenDelay = 1f; }),
            };
        }

        Health Target(string label, Vector3 pos, Color color, System.Action<Health> setup)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = label;
            body.transform.position = pos;
            Tint(body, color);

            var h = body.AddComponent<Health>();
            setup(h);
            h.onDeath = Health.DeathAction.None;
            h.Died += _ => StartCoroutine(ReviveLater(h, body));
            body.AddComponent<HealthBar>();

            // Kafa: ayrı collider + Hitbox (2x kritik)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(body.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            head.transform.localScale = Vector3.one * 0.6f;
            Tint(head, color * 0.8f);
            var hb = head.AddComponent<Hitbox>();
            hb.multiplier = 2f;
            hb.critical = true;
            return h;
        }

        IEnumerator ReviveLater(Health h, GameObject body)
        {
            body.transform.localScale = new Vector3(1f, 0.3f, 1f); // "yere yığıldı"
            yield return new WaitForSeconds(3f);
            body.transform.localScale = Vector3.one;
            h.Revive();
        }

        IEnumerator FireLoop()
        {
            int i = 0;
            while (true)
            {
                yield return new WaitForSeconds(fireInterval);
                var t = targets[i++ % targets.Length];
                if (t == null || t.IsDead) continue;
                bool headshot = Random.value < 0.3f;
                bool isFire = Random.value < 0.5f;
                var aim = t.transform.position + (headshot ? Vector3.up * 1.2f : Vector3.zero);
                Shoot(aim, isFire);
            }
        }

        void Shoot(Vector3 aim, bool isFire)
        {
            var origin = turret.position + Vector3.up * 0.6f;
            var dir = (aim - origin).normalized;
            turret.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));

            var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            p.name = isFire ? "Fireball" : "Bullet";
            p.transform.position = origin + dir;
            p.transform.localScale = Vector3.one * 0.3f;
            Tint(p, isFire ? fire.color : Color.yellow);
            p.GetComponent<Collider>().isTrigger = true;

            var rb = p.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = dir * projectileSpeed;

            var dmg = p.AddComponent<DamageOnContact>();
            dmg.damage = 12f;
            dmg.damageType = isFire ? fire : null;
            dmg.destroyOnHit = true;
            dmg.source = turret.gameObject;
            Destroy(p, 3f);
        }

        IEnumerator ExplosionLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(explosionInterval);
                var center = new Vector3(Random.Range(-5f, 5f), 0.5f, 4.5f);
                int hits = Damage.Explosion(center, 4f, new DamageInfo(60f, fire, gameObject), falloff: 0.8f);
                Debug.Log($"<color=orange>BOOM</color> at {center:F1}: {hits} hedef");
                StartCoroutine(Flash(center, 4f));
            }
        }

        IEnumerator Flash(Vector3 center, float radius)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(s.GetComponent<Collider>());
            s.transform.position = center;
            Tint(s, new Color(1f, 0.5f, 0.1f));
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                s.transform.localScale = Vector3.one * (radius * 2f * t / 0.3f);
                yield return null;
            }
            Destroy(s);
        }

        static void LogDamage(DamageResult r)
        {
            string type = r.info.type ? r.info.type.DisplayName : "physical";
            Debug.Log($"{r.health.name}: -{r.applied:0.#} ({type}{(r.info.critical ? ", CRIT" : "")}) → {r.health.Current:0}/{r.health.Max:0}");
        }

        static void LogDeath(DamageResult r) => Debug.Log($"<color=red>{r.health.name} öldü</color> (öldüren: {(r.info.source ? r.info.source.name : "?")})");

        static void Tint(GameObject go, Color c)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // Built-in
            rend.SetPropertyBlock(block);
        }
    }
}
