using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Atthree.Combat
{
    /// <summary>
    /// Can bileşeni. Hasar şu sırayla işlenir:
    ///   ölü/kapalı? → dost ateşi? → dokunulmazlık? → BeforeDamage (kalkan, blok) → direnç × çarpan → zırh → candan düş
    ///
    ///   health.TakeDamage(new DamageInfo(20f));
    ///   health.Heal(10f);
    ///   health.Died += r =&gt; Debug.Log(r.info.source + " öldürdü");
    /// </summary>
    [AddComponentMenu("Atthree/Combat/Health")]
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        public enum DeathAction { None, Deactivate, Destroy }

        [Serializable]
        public struct Resistance
        {
            public DamageType type;
            [Tooltip("Hasar çarpanı: 0 = bağışık, 0.5 = dirençli, 1 = normal, 2 = zayıf")]
            [Min(0f)] public float multiplier;
        }

        [Header("Can")]
        [Min(1f)] [SerializeField] float maxHealth = 100f;
        [Tooltip("Başlangıç canı. 0 ya da max'tan büyükse tam canla başlar.")]
        [Min(0f)] [SerializeField] float startHealth;

        [Header("Savunma")]
        [Tooltip("Her vuruştan düz düşülen miktar.")]
        [Min(0f)] public float armor;
        [Tooltip("Zırh hasarı bundan aşağı indiremez (zırh yüzünden hiç hasar almama olmasın). 0 = kapalı.")]
        [Min(0f)] public float minimumDamage = 1f;
        [Tooltip("Tüm gelen hasarın genel çarpanı (zorluk seviyesi, buff).")]
        [Min(0f)] public float damageMultiplier = 1f;
        public List<Resistance> resistances = new List<Resistance>();

        [Header("Dokunulmazlık")]
        [Tooltip("Vurulduktan sonra bu kadar saniye yeni hasar almaz (i-frame). 0 = kapalı.")]
        [Min(0f)] public float invulnerabilityAfterHit;
        [Tooltip("Hiç hasar almaz (god mode, ara sahne).")]
        public bool invulnerable;

        [Header("Takım")]
        [Tooltip("Aynı takımdan gelen hasar engellenir (dost ateşi kapalıysa). -1 = takımsız.")]
        public int team = DamageInfo.NoTeam;
        public bool friendlyFire;

        [Header("Yenilenme")]
        [Tooltip("Saniyede geri gelen can. 0 = kapalı.")]
        [Min(0f)] public float regenPerSecond;
        [Tooltip("Son hasardan sonra yenilenmenin başlaması için beklenecek süre.")]
        [Min(0f)] public float regenDelay = 3f;

        [Header("Ölüm")]
        public DeathAction onDeath = DeathAction.None;
        [Min(0f)] public float deathActionDelay;

        [Header("Olaylar (Inspector)")]
        [Tooltip("Uygulanan hasar miktarı")]
        public UnityEvent<float> onDamaged = new UnityEvent<float>();
        [Tooltip("İyileşen miktar")]
        public UnityEvent<float> onHealed = new UnityEvent<float>();
        [Tooltip("Can oranı 0..1 (can barı için)")]
        public UnityEvent<float> onHealthChanged = new UnityEvent<float>();
        public UnityEvent onDied = new UnityEvent();
        public UnityEvent onRevived = new UnityEvent();

        // ------------------------------------------------------------------ kod olayları

        /// <summary>Hasar candan düştükten sonra.</summary>
        public event Action<DamageResult> Damaged;
        /// <summary>Hasar engellendiğinde (dokunulmazlık, bağışıklık, dost ateşi...). "Tink" sesi, "IMMUNE" yazısı için.</summary>
        public event Action<DamageResult> DamageBlocked;
        public event Action<float> Healed;
        /// <summary>(mevcut, max) her değişimde.</summary>
        public event Action<float, float> Changed;
        public event Action<DamageResult> Died;
        public event Action Revived;

        /// <summary>
        /// Hasar uygulanmadan önce çağrılır; miktarı değiştirebilir (kalkan, blok, kritik bonus...).
        /// 0'a indirirsen hasar "Modified" olarak engellenir.
        /// </summary>
        public event DamageModifier BeforeDamage;
        public delegate void DamageModifier(Health health, ref DamageInfo info);

        /// <summary>Sahnedeki herhangi bir Health hasar aldığında (skor, hasar sayıları, kamera sarsıntısı...).</summary>
        public static event Action<DamageResult> AnyDamaged;
        /// <summary>Sahnedeki herhangi bir Health öldüğünde.</summary>
        public static event Action<DamageResult> AnyDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { AnyDamaged = null; AnyDied = null; }

        // ------------------------------------------------------------------ durum

        float current;
        bool initialized;
        float invulnerableUntil = float.NegativeInfinity;
        float lastDamageTime = float.NegativeInfinity;

        public float Current { get { Init(); return current; } }
        public float Max => maxHealth;
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsDead { get; private set; }
        public bool IsAlive => !IsDead;
        public bool IsFull => Current >= Max;
        /// <summary>Şu an hasar alamaz mı (god mode ya da i-frame)?</summary>
        public bool IsInvulnerable => invulnerable || Time.time < invulnerableUntil;
        /// <summary>Son ölümcül vuruş (ölmediyse default).</summary>
        public DamageResult LastDeath { get; private set; }

        void Awake() => Init();

        void Init()
        {
            if (initialized) return;
            initialized = true;
            current = startHealth > 0f && startHealth <= maxHealth ? startHealth : maxHealth;
        }

        void Update()
        {
            if (IsDead || regenPerSecond <= 0f || current >= maxHealth) return;
            if (Time.time - lastDamageTime < regenDelay) return;
            SetCurrent(Mathf.Min(maxHealth, current + regenPerSecond * Time.deltaTime), notifyHeal: false);
        }

        // ------------------------------------------------------------------ hasar

        public DamageResult TakeDamage(float amount, GameObject source = null) => TakeDamage(new DamageInfo(amount, null, source));

        public DamageResult TakeDamage(DamageInfo info)
        {
            Init();
            if (!isActiveAndEnabled) return DamageResult.Blocked(this, info, DamageBlock.Disabled);
            if (IsDead) return Block(info, DamageBlock.Dead);
            if (info.amount <= 0f) return Block(info, DamageBlock.Immune);

            if (!friendlyFire && team != DamageInfo.NoTeam && info.team == team)
                return Block(info, DamageBlock.FriendlyFire);

            if (!info.ignoreInvulnerability && IsInvulnerable)
                return Block(info, DamageBlock.Invulnerable);

            float incoming = info.amount;
            if (BeforeDamage != null)
            {
                foreach (DamageModifier mod in BeforeDamage.GetInvocationList())
                {
                    try { mod(this, ref info); }
                    catch (Exception e) { Debug.LogException(e, this); }
                }
                if (info.amount <= 0f)
                {
                    var r = DamageResult.Blocked(this, info, DamageBlock.Modified);
                    r.incoming = incoming;
                    DamageBlocked?.Invoke(r);
                    return r;
                }
            }

            float dmg = info.amount * damageMultiplier * ResistanceFor(info.type);
            if (dmg <= 0f) return Block(info, DamageBlock.Immune);

            if (!info.ignoreArmor && armor > 0f)
            {
                float afterArmor = dmg - armor;
                dmg = minimumDamage > 0f ? Mathf.Max(afterArmor, Mathf.Min(dmg, minimumDamage)) : afterArmor;
                if (dmg <= 0f) return Block(info, DamageBlock.Immune);
            }

            float applied = Mathf.Min(current, dmg);
            lastDamageTime = Time.time;
            if (invulnerabilityAfterHit > 0f) invulnerableUntil = Time.time + invulnerabilityAfterHit;

            var result = new DamageResult { health = this, info = info, incoming = incoming, applied = applied };
            SetCurrent(current - applied, notifyHeal: false);
            result.killed = current <= 0f;

            Damaged?.Invoke(result);
            onDamaged.Invoke(applied);
            AnyDamaged?.Invoke(result);

            if (result.killed) Die(result);
            return result;
        }

        DamageResult Block(DamageInfo info, DamageBlock reason)
        {
            var r = DamageResult.Blocked(this, info, reason);
            DamageBlocked?.Invoke(r);
            return r;
        }

        /// <summary>Bu tür için toplam direnç çarpanı (listede yoksa 1).</summary>
        public float ResistanceFor(DamageType type)
        {
            if (type == null) return 1f;
            float m = 1f;
            foreach (var r in resistances)
                if (r.type == type) m *= r.multiplier;
            return m;
        }

        /// <summary>Dirence bakılmaksızın anında öldür.</summary>
        public void Kill(GameObject source = null)
        {
            Init();
            if (IsDead) return;
            var info = new DamageInfo(current, null, source) { ignoreArmor = true, ignoreInvulnerability = true };
            var result = new DamageResult { health = this, info = info, incoming = current, applied = current, killed = true };
            SetCurrent(0f, notifyHeal: false);
            Damaged?.Invoke(result);
            onDamaged.Invoke(result.applied);
            AnyDamaged?.Invoke(result);
            Die(result);
        }

        void Die(DamageResult result)
        {
            IsDead = true;
            LastDeath = result;
            Died?.Invoke(result);
            onDied.Invoke();
            AnyDied?.Invoke(result);

            switch (onDeath)
            {
                case DeathAction.Deactivate:
                    if (deathActionDelay > 0f) Invoke(nameof(DeactivateNow), deathActionDelay);
                    else DeactivateNow();
                    break;
                case DeathAction.Destroy:
                    Destroy(gameObject, deathActionDelay);
                    break;
            }
        }

        void DeactivateNow()
        {
            if (IsDead) gameObject.SetActive(false); // gecikme sırasında dirilmiş olabilir
        }

        // ------------------------------------------------------------------ iyileşme

        /// <summary>Can ekler (max'ı geçmez). Ölüyken işe yaramaz, onun için <see cref="Revive"/>. İyileşen miktarı döndürür.</summary>
        public float Heal(float amount)
        {
            Init();
            if (IsDead || amount <= 0f || current >= maxHealth) return 0f;
            float before = current;
            SetCurrent(Mathf.Min(maxHealth, current + amount), notifyHeal: true);
            return current - before;
        }

        /// <summary>Ölüyü dirilt. <paramref name="healthFraction"/>: max canın kaçta kaçıyla (0..1].</summary>
        public void Revive(float healthFraction = 1f)
        {
            Init();
            if (!IsDead) return;
            CancelInvoke(nameof(DeactivateNow));
            IsDead = false;
            LastDeath = default;
            lastDamageTime = float.NegativeInfinity;
            invulnerableUntil = float.NegativeInfinity;
            SetCurrent(Mathf.Max(0.01f, maxHealth * Mathf.Clamp01(healthFraction)), notifyHeal: false);
            Revived?.Invoke();
            onRevived.Invoke();
        }

        /// <summary>Max canı değiştir. <paramref name="keepRatio"/>: oran korunsun mu (50/100 → 100/200)?</summary>
        public void SetMaxHealth(float value, bool keepRatio = true)
        {
            Init();
            value = Mathf.Max(1f, value);
            float ratio = Normalized;
            maxHealth = value;
            float next = keepRatio ? value * ratio : Mathf.Min(current, value);
            if (!IsDead) next = Mathf.Max(next, 0.01f);
            SetCurrent(next, notifyHeal: false);
        }

        /// <summary>Canı doğrudan ayarla (kayıt yükleme vb.). 0 öldürmez; öldürmek için Kill.</summary>
        public void SetHealth(float value)
        {
            Init();
            if (IsDead) return;
            SetCurrent(Mathf.Clamp(value, 0.01f, maxHealth), notifyHeal: false);
        }

        /// <summary>Belirli bir süre dokunulmaz yap (dash, yeniden doğma). Mevcut süreden uzunsa uzatır.</summary>
        public void SetInvulnerableFor(float seconds) =>
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);

        void SetCurrent(float value, bool notifyHeal)
        {
            float before = current;
            current = value;
            if (Mathf.Approximately(before, current)) return;
            if (notifyHeal && current > before)
            {
                Healed?.Invoke(current - before);
                onHealed.Invoke(current - before);
            }
            Changed?.Invoke(current, maxHealth);
            onHealthChanged.Invoke(Normalized);
        }

        void OnValidate()
        {
            if (startHealth > maxHealth) startHealth = maxHealth;
            if (Application.isPlaying && initialized && current > maxHealth) SetCurrent(maxHealth, false);
        }
    }
}
