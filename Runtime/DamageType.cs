using UnityEngine;

namespace Atthree.Combat
{
    /// <summary>
    /// Hasar türü asset'i (Ateş, Zehir, Fiziksel...). Health'teki dirençler bu asset'lere göre çalışır.
    /// Create > Atthree > Combat > Damage Type
    /// </summary>
    [CreateAssetMenu(menuName = "Atthree/Combat/Damage Type", fileName = "DamageType", order = 0)]
    public class DamageType : ScriptableObject
    {
        [Tooltip("Arayüzde görünecek ad. Boşsa asset adı kullanılır.")]
        public string displayName;

        [Tooltip("Hasar sayıları, efektler vb. için önerilen renk.")]
        public Color color = Color.white;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    }
}
