using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Atthree.Combat.Tests
{
    public class HealthTests
    {
        readonly List<Object> cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in cleanup) if (o != null) Object.Destroy(o);
            cleanup.Clear();
        }

        T Track<T>(T o) where T : Object { cleanup.Add(o); return o; }

        Health Make(float max = 100f, Vector3? pos = null, PrimitiveType? shape = null)
        {
            var go = shape.HasValue ? GameObject.CreatePrimitive(shape.Value) : new GameObject("target");
            Track(go);
            go.transform.position = pos ?? Vector3.zero;
            var h = go.AddComponent<Health>();
            h.SetMaxHealth(max, keepRatio: true);
            h.minimumDamage = 0f;
            return h;
        }

        DamageType Type(string n)
        {
            var t = Track(ScriptableObject.CreateInstance<DamageType>());
            t.name = n;
            return t;
        }

        // ------------------------------------------------------------------ temel

        [Test]
        public void StartsFull_AndTakesDamage()
        {
            var h = Make();
            Assert.AreEqual(100f, h.Current);
            var r = h.TakeDamage(new DamageInfo(30f));
            Assert.AreEqual(30f, r.applied);
            Assert.AreEqual(70f, h.Current);
            Assert.AreEqual(0.7f, h.Normalized, 1e-4f);
            Assert.IsFalse(r.WasBlocked);
        }

        [Test]
        public void Overkill_AppliesOnlyRemaining_AndDiesOnce()
        {
            var h = Make();
            int died = 0;
            h.Died += _ => died++;
            var r = h.TakeDamage(new DamageInfo(250f));
            Assert.AreEqual(100f, r.applied);
            Assert.IsTrue(r.killed);
            Assert.IsTrue(h.IsDead);

            var again = h.TakeDamage(new DamageInfo(10f));
            Assert.AreEqual(DamageBlock.Dead, again.blocked);
            h.Kill();
            Assert.AreEqual(1, died);
        }

        [Test]
        public void ArmorSubtracts_WithMinimumDamage()
        {
            var h = Make();
            h.armor = 5f;
            Assert.AreEqual(15f, h.TakeDamage(new DamageInfo(20f)).applied);

            h.minimumDamage = 1f;
            Assert.AreEqual(1f, h.TakeDamage(new DamageInfo(3f)).applied, "zırh minimumun altına indiremez");

            h.minimumDamage = 0f;
            Assert.AreEqual(DamageBlock.Immune, h.TakeDamage(new DamageInfo(3f)).blocked);

            Assert.AreEqual(3f, h.TakeDamage(new DamageInfo(3f) { ignoreArmor = true }).applied);
        }

        [Test]
        public void Resistances_MultiplyByType()
        {
            var fire = Type("Fire");
            var poison = Type("Poison");
            var ice = Type("Ice");
            var h = Make();
            h.resistances.Add(new Health.Resistance { type = fire, multiplier = 0.5f });
            h.resistances.Add(new Health.Resistance { type = poison, multiplier = 0f });

            Assert.AreEqual(10f, h.TakeDamage(new DamageInfo(20f, fire)).applied);
            Assert.AreEqual(DamageBlock.Immune, h.TakeDamage(new DamageInfo(20f, poison)).blocked);
            Assert.AreEqual(20f, h.TakeDamage(new DamageInfo(20f, ice)).applied, "listede olmayan tür normal");
            Assert.AreEqual(20f, h.TakeDamage(new DamageInfo(20f)).applied, "türsüz hasar normal");
        }

        [UnityTest]
        public IEnumerator InvulnerabilityFrames_BlockThenExpire()
        {
            var h = Make();
            h.invulnerabilityAfterHit = 0.2f;
            Assert.IsFalse(h.TakeDamage(new DamageInfo(10f)).WasBlocked);
            Assert.AreEqual(DamageBlock.Invulnerable, h.TakeDamage(new DamageInfo(10f)).blocked);
            Assert.IsFalse(h.TakeDamage(new DamageInfo(10f) { ignoreInvulnerability = true }).WasBlocked);

            yield return new WaitForSeconds(0.25f);
            Assert.IsFalse(h.TakeDamage(new DamageInfo(10f)).WasBlocked);
            Assert.AreEqual(70f, h.Current);
        }

        [Test]
        public void FriendlyFire_BlockedForSameTeam()
        {
            var h = Make();
            h.team = 1;
            Assert.AreEqual(DamageBlock.FriendlyFire, h.TakeDamage(new DamageInfo(10f) { team = 1 }).blocked);
            Assert.IsFalse(h.TakeDamage(new DamageInfo(10f) { team = 2 }).WasBlocked);
            Assert.IsFalse(h.TakeDamage(new DamageInfo(10f)).WasBlocked, "takımsız hasar herkese işler");
            h.friendlyFire = true;
            Assert.IsFalse(h.TakeDamage(new DamageInfo(10f) { team = 1 }).WasBlocked);
        }

        [Test]
        public void BeforeDamage_ModifierCanShield()
        {
            var h = Make();
            float shield = 15f;
            h.BeforeDamage += (Health _, ref DamageInfo info) =>
            {
                float absorbed = Mathf.Min(shield, info.amount);
                shield -= absorbed;
                info.amount -= absorbed;
            };
            var r1 = h.TakeDamage(new DamageInfo(10f));
            Assert.AreEqual(DamageBlock.Modified, r1.blocked);
            Assert.AreEqual(100f, h.Current);
            var r2 = h.TakeDamage(new DamageInfo(10f));
            Assert.AreEqual(5f, r2.applied, "kalkanın kalan 5'i emildi");
        }

        [Test]
        public void Heal_ClampsToMax_AndNotWhileDead()
        {
            var h = Make();
            float healed = 0f;
            h.Healed += a => healed += a;
            h.TakeDamage(new DamageInfo(30f));
            Assert.AreEqual(30f, h.Heal(50f));
            Assert.AreEqual(100f, h.Current);
            Assert.AreEqual(30f, healed);
            Assert.AreEqual(0f, h.Heal(10f), "tam canda iyileşme yok");

            h.Kill();
            Assert.AreEqual(0f, h.Heal(10f));
            Assert.IsTrue(h.IsDead);
        }

        [Test]
        public void Revive_RestoresFraction_AndFiresEvent()
        {
            var h = Make();
            bool revived = false;
            h.Revived += () => revived = true;
            h.Kill();
            h.Revive(0.5f);
            Assert.IsTrue(revived);
            Assert.IsFalse(h.IsDead);
            Assert.AreEqual(50f, h.Current);
            Assert.IsFalse(h.TakeDamage(new DamageInfo(10f)).WasBlocked);
        }

        [Test]
        public void SetMaxHealth_KeepsRatioOrClamps()
        {
            var h = Make();
            h.TakeDamage(new DamageInfo(50f));
            h.SetMaxHealth(200f, keepRatio: true);
            Assert.AreEqual(100f, h.Current);
            h.SetMaxHealth(60f, keepRatio: false);
            Assert.AreEqual(60f, h.Current);
            Assert.AreEqual(60f, h.Max);
        }

        [UnityTest]
        public IEnumerator Regen_StartsAfterDelay()
        {
            var h = Make();
            h.regenPerSecond = 100f;
            h.regenDelay = 0.3f;
            h.TakeDamage(new DamageInfo(50f));
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(50f, h.Current, "gecikme bitmeden yenilenmemeli");
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(h.Current, 50f);
            Assert.LessOrEqual(h.Current, 100f);
        }

        [Test]
        public void StaticEvents_ReportAnyHealth()
        {
            var a = Make();
            var b = Make();
            int damaged = 0, died = 0;
            System.Action<DamageResult> onD = _ => damaged++, onDie = _ => died++;
            Health.AnyDamaged += onD;
            Health.AnyDied += onDie;
            try
            {
                a.TakeDamage(new DamageInfo(10f));
                b.TakeDamage(new DamageInfo(500f));
            }
            finally
            {
                Health.AnyDamaged -= onD;
                Health.AnyDied -= onDie;
            }
            Assert.AreEqual(2, damaged);
            Assert.AreEqual(1, died);
        }

        [UnityTest]
        public IEnumerator DeathAction_Destroy()
        {
            var h = Make();
            h.onDeath = Health.DeathAction.Destroy;
            var go = h.gameObject;
            h.Kill();
            yield return null;
            Assert.IsTrue(go == null);
        }

        // ------------------------------------------------------------------ hitbox / yardımcılar

        [Test]
        public void Hitbox_ForwardsWithMultiplier_AndCritical()
        {
            var h = Make();
            var head = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            head.transform.SetParent(h.transform);
            var hb = head.AddComponent<Hitbox>();
            hb.multiplier = 2f;
            hb.critical = true;

            DamageResult got = default;
            h.Damaged += r => got = r;
            Assert.IsTrue(Damage.Apply(head.GetComponent<Collider>(), new DamageInfo(10f)));
            Assert.AreEqual(20f, got.applied);
            Assert.IsTrue(got.info.critical);
            Assert.AreEqual(80f, h.Current);
        }

        [Test]
        public void DamageApply_FindsHealthInParent_ReturnsFalseOtherwise()
        {
            var h = Make();
            var child = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            child.transform.SetParent(h.transform);
            Assert.IsTrue(Damage.Apply(child.GetComponent<Collider>(), new DamageInfo(5f)));
            Assert.AreEqual(95f, h.Current);

            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Assert.IsFalse(Damage.Apply(wall.GetComponent<Collider>(), new DamageInfo(5f)));
        }

        [UnityTest]
        public IEnumerator Explosion_FalloffAndOneHitPerHealth()
        {
            var near = Make(100f, new Vector3(0f, 0f, 0f), PrimitiveType.Cube);
            // aynı Health'e ait ikinci collider: tek vuruş sayılmalı
            var extra = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            extra.transform.SetParent(near.transform);
            extra.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            var far = Make(100f, new Vector3(6f, 0f, 0f), PrimitiveType.Cube);
            var outside = Make(100f, new Vector3(30f, 0f, 0f), PrimitiveType.Cube);
            yield return new WaitForFixedUpdate();

            var results = new List<DamageResult>();
            int hits = Damage.Explosion(new Vector3(-1f, 0f, 0f), 10f, new DamageInfo(100f), falloff: 1f, results: results);

            Assert.AreEqual(2, hits);
            Assert.AreEqual(2, results.Count);
            Assert.Less(near.Current, far.Current, "yakın olan daha çok hasar almalı");
            Assert.Less(far.Current, 100f);
            Assert.AreEqual(100f, outside.Current);
        }

        [UnityTest]
        public IEnumerator DamageOnContact_Trigger_HitsOnce_AndDestroys()
        {
            var h = Make(100f, Vector3.zero, PrimitiveType.Cube);
            var proj = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            proj.transform.position = new Vector3(0f, 0f, -3f);
            proj.transform.localScale = Vector3.one * 0.3f;
            proj.GetComponent<Collider>().isTrigger = true;
            var rb = proj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = Vector3.forward * 10f;
            var d = proj.AddComponent<DamageOnContact>();
            d.damage = 25f;
            d.destroyOnHit = true;

            for (int i = 0; i < 40 && proj != null; i++) yield return new WaitForFixedUpdate();

            Assert.IsTrue(proj == null, "mermi vurunca yok olmalı");
            Assert.AreEqual(75f, h.Current);
        }

        [UnityTest]
        public IEnumerator DamageOnContact_RepeatInterval_WhileInside()
        {
            var h = Make(100f, Vector3.zero, PrimitiveType.Cube);
            h.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            var lava = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            lava.transform.localScale = Vector3.one * 3f;
            lava.GetComponent<Collider>().isTrigger = true;
            var d = lava.AddComponent<DamageOnContact>();
            d.damage = 10f;
            d.repeatInterval = 0.1f;

            yield return new WaitForSeconds(0.45f);
            float lost = 100f - h.Current;
            Assert.GreaterOrEqual(lost, 30f, "en az 3-4 kez vurmalı");
            Assert.LessOrEqual(lost, 60f, "aralıktan sık vurmamalı");
        }

        [Test]
        public void DamageOnContact_IgnoresOwnHierarchy()
        {
            var h = Make();
            var sword = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            sword.transform.SetParent(h.transform);
            var d = sword.AddComponent<DamageOnContact>();
            // Fizik beklemeden çağır: OnTriggerEnter'ı mesajla tetikle
            sword.SendMessage("OnTriggerEnter", h.gameObject.AddComponent<BoxCollider>());
            Assert.AreEqual(100f, h.Current);
        }

        // ------------------------------------------------------------------ can barı

        [UnityTest]
        public IEnumerator HealthBar_AutoBuild_TracksHealth()
        {
            var h = Make();
            var bar = h.gameObject.AddComponent<HealthBar>();
            yield return null;
            Assert.IsNotNull(bar.fill, "auto build fill oluşturmalı");
            Assert.AreEqual(1f, bar.fill.anchorMax.x, 1e-4f);

            h.TakeDamage(new DamageInfo(40f));
            Assert.AreEqual(0.6f, bar.fill.anchorMax.x, 1e-4f);
            Assert.AreEqual(1f, bar.ghost.anchorMax.x, 1e-4f, "ghost hemen düşmemeli");

            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0.6f, bar.ghost.anchorMax.x, 1e-3f, "ghost zamanla yetişmeli");

            var barRoot = bar.fill.root.gameObject;
            Object.Destroy(h.gameObject);
            yield return null;
            yield return null;
            Assert.IsTrue(barRoot == null, "Health yok olunca dünya barı da silinmeli");
        }
    }
}
