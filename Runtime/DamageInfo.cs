using UnityEngine;

namespace Atthree.Combat
{
    /// <summary>
    /// Bir hasar olayının tüm bilgisi. Sadece miktar zorunlu, gerisi isteğe bağlı:
    ///
    ///   target.TakeDamage(new DamageInfo(25f));
    ///   target.TakeDamage(new DamageInfo(25f, fireType, gameObject) { point = hit.point, direction = ray.direction });
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        /// <summary>Takımsız (herkese vurabilir).</summary>
        public const int NoTeam = -1;

        [Tooltip("Ham hasar (zırh/direnç öncesi).")]
        public float amount;

        [Tooltip("Boşsa türsüz hasar: dirençler uygulanmaz.")]
        public DamageType type;

        /// <summary>Hasarı veren obje (oyuncu, mermi, tuzak). Puan/öldüren bilgisi için.</summary>
        public GameObject source;

        /// <summary>Vuruş noktası (dünya). Efekt ve hasar sayısı için.</summary>
        public Vector3 point;

        /// <summary>Vuruş yönü (normalize). Geri itme ve efekt yönü için.</summary>
        public Vector3 direction;

        /// <summary>Kritik vuruş mu? (Hitbox kafa vuruşunda işaretler.)</summary>
        public bool critical;

        /// <summary>Dokunulmazlık süresini (i-frame) yok say.</summary>
        public bool ignoreInvulnerability;

        /// <summary>Zırhı yok say (dirençler yine uygulanır).</summary>
        public bool ignoreArmor;

        /// <summary>Hasarı verenin takımı. Aynı takımdaki Health'ler (dost ateşi kapalıysa) hasar almaz.</summary>
        public int team;

        public DamageInfo(float amount, DamageType type = null, GameObject source = null)
        {
            this.amount = amount;
            this.type = type;
            this.source = source;
            point = Vector3.zero;
            direction = Vector3.zero;
            critical = false;
            ignoreInvulnerability = false;
            ignoreArmor = false;
            team = NoTeam;
        }

        public override string ToString() =>
            $"{amount:0.#} {(type ? type.DisplayName : "")}{(critical ? " CRIT" : "")}{(source ? " from " + source.name : "")}";
    }

    /// <summary>Hasar neden uygulanmadı?</summary>
    public enum DamageBlock
    {
        None,
        /// <summary>Zaten ölü.</summary>
        Dead,
        /// <summary>Dokunulmazlık süresi içinde.</summary>
        Invulnerable,
        /// <summary>Direnç 0 (bağışık) ya da zırh hepsini yuttu.</summary>
        Immune,
        /// <summary>Aynı takım, dost ateşi kapalı.</summary>
        FriendlyFire,
        /// <summary>BeforeDamage değiştiricisi hasarı sıfırladı (kalkan, blok).</summary>
        Modified,
        /// <summary>Health bileşeni kapalı ya da yok.</summary>
        Disabled,
    }

    /// <summary>TakeDamage'in sonucu: ne kadar hasar gitti, öldü mü, engellendiyse neden.</summary>
    public struct DamageResult
    {
        public Health health;
        public DamageInfo info;
        /// <summary>Gelen ham hasar (Hitbox çarpanı dahil).</summary>
        public float incoming;
        /// <summary>Canından gerçekten düşen miktar.</summary>
        public float applied;
        /// <summary>Bu vuruş öldürdü mü?</summary>
        public bool killed;
        public DamageBlock blocked;

        public bool WasBlocked => blocked != DamageBlock.None;

        public static DamageResult Blocked(Health h, DamageInfo info, DamageBlock reason) =>
            new DamageResult { health = h, info = info, incoming = info.amount, blocked = reason };
    }

    /// <summary>Hasar alabilen her şey: Health, Hitbox ya da kendi sınıfın (kırılabilir kutu, kalkan...).</summary>
    public interface IDamageable
    {
        DamageResult TakeDamage(DamageInfo info);
    }
}
