using UnityEngine;
using UnityEngine.UI;

namespace Atthree.Combat
{
    /// <summary>
    /// Can barı. İki kullanım:
    ///  • Dünyada (düşman başı): karaktere ekle, "Auto Build" açık kalsın; bar kendi Canvas'ını kurar, kameraya döner.
    ///  • Ekranda (HUD): kendi UI'ını kur, Auto Build'i kapat, Fill (ve istersen Ghost) RectTransform'larını ata.
    ///    Fill objesi sola dayalı, yatayda gerilmiş (anchor min 0,0 max 1,1) olmalı; bar genişliği anchorMax.x ile ayarlanır.
    /// Ghost: hasardan sonra arkada gecikmeli eriyen açık renkli parça (ne kadar can gittiğini gösterir).
    /// </summary>
    [AddComponentMenu("Atthree/Combat/Health Bar")]
    public class HealthBar : MonoBehaviour
    {
        [Tooltip("Boşsa bu objede ya da üstlerinde aranır.")]
        [SerializeField] Health health;

        [Header("Otomatik kurulum (dünya barı)")]
        public bool autoBuild = true;
        [Tooltip("Barın objeye göre konumu (yerel değil, dünya ekseninde).")]
        public Vector3 offset = new Vector3(0f, 2.2f, 0f);
        public Vector2 size = new Vector2(1.2f, 0.14f);
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        public Color ghostColor = new Color(1f, 0.9f, 0.6f, 0.9f);
        [Tooltip("Can oranına göre renk: sol = ölü, sağ = tam.")]
        public Gradient fillColor = DefaultGradient();
        [Tooltip("Her zaman kameraya dönsün.")]
        public bool billboard = true;

        [Header("Elle bağlama (HUD)")]
        public RectTransform fill;
        public RectTransform ghost;
        [Tooltip("Rengi değişecek Graphic (boşsa Fill üzerindeki).")]
        public Graphic fillGraphic;

        [Header("Davranış")]
        [Tooltip("Ghost'un erimeye başlamadan önce bekleyeceği süre.")]
        [Min(0f)] public float ghostDelay = 0.4f;
        [Tooltip("Ghost'un saniyede eriyeceği oran (1 = tam bar/sn).")]
        [Min(0.01f)] public float ghostSpeed = 0.8f;
        [Tooltip("Can tamken gizle (düşman barları için).")]
        public bool hideWhenFull;
        [Tooltip("Ölünce gizle.")]
        public bool hideWhenDead = true;

        Canvas canvas;
        CanvasGroup group;
        Transform root;
        float shown = 1f, ghostValue = 1f, ghostWait;
        Camera cam;

        public Health Health
        {
            get => health;
            set
            {
                if (health != null) health.Changed -= OnChanged;
                health = value;
                if (health != null && isActiveAndEnabled) { health.Changed += OnChanged; Snap(); }
            }
        }

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.85f, 0.15f, 0.15f), 0f), new GradientColorKey(new Color(0.95f, 0.75f, 0.1f), 0.5f), new GradientColorKey(new Color(0.25f, 0.85f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (autoBuild && fill == null) Build();
            if (fillGraphic == null && fill != null) fillGraphic = fill.GetComponent<Graphic>();
            if (canvas == null) group = GetComponent<CanvasGroup>(); // HUD: gizlemek için CanvasGroup varsa kullan
        }

        void OnEnable()
        {
            if (health != null) health.Changed += OnChanged;
            Snap();
        }

        void OnDisable()
        {
            if (health != null) health.Changed -= OnChanged;
            if (canvas != null) canvas.enabled = false;
        }

        void OnDestroy()
        {
            // Dünya barı ayrı bir kök objede (karakterle dönmesin diye); onu da temizle.
            if (root != null && root != transform) Destroy(root.gameObject);
        }

        void Snap()
        {
            shown = ghostValue = health != null ? health.Normalized : 1f;
            Apply();
        }

        void OnChanged(float current, float max)
        {
            float n = max > 0f ? current / max : 0f;
            if (n < shown) ghostWait = ghostDelay;      // hasar: ghost bekleyip erisin
            else ghostValue = Mathf.Max(ghostValue, n); // iyileşme: ghost hemen yetişsin
            shown = n;
            Apply();
        }

        void LateUpdate()
        {
            if (health == null) return;

            if (ghostValue > shown)
            {
                if (ghostWait > 0f) ghostWait -= Time.deltaTime;
                else ghostValue = Mathf.Max(shown, ghostValue - ghostSpeed * Time.deltaTime);
                SetWidth(ghost, ghostValue);
            }

            if (root != null && root != transform)
            {
                root.position = health.transform.position + offset;
                if (billboard)
                {
                    if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
                    if (cam != null) root.rotation = cam.transform.rotation;
                }
            }

            UpdateVisibility();
        }

        void Apply()
        {
            SetWidth(fill, shown);
            SetWidth(ghost, ghostValue);
            if (fillGraphic != null) fillGraphic.color = fillColor.Evaluate(shown);
            UpdateVisibility();
        }

        void UpdateVisibility()
        {
            if (health == null) return;
            bool visible = !(hideWhenDead && health.IsDead) && !(hideWhenFull && health.IsFull && ghostValue >= 1f);
            if (canvas != null) canvas.enabled = visible && isActiveAndEnabled;
            else if (group != null) group.alpha = visible ? 1f : 0f;
        }

        static void SetWidth(RectTransform rt, float n)
        {
            if (rt == null) return;
            var max = rt.anchorMax;
            max.x = Mathf.Clamp01(n);
            rt.anchorMax = max;
        }

        /// <summary>Dünya-uzayı Canvas + arka plan + ghost + dolgu kurar.</summary>
        void Build()
        {
            var go = new GameObject(name + " Health Bar", typeof(RectTransform));
            root = go.transform;
            root.position = (health ? health.transform.position : transform.position) + offset;

            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            var crt = (RectTransform)root;
            // 100 birim = size; UI çözünürlüğü için küçük ölçek.
            crt.sizeDelta = size * 100f;
            crt.localScale = Vector3.one * 0.01f;

            var bg = Rect("Background", crt, backgroundColor);
            Stretch(bg, 0f);
            // Kenar boşluğu için iç kap; dolgular onun içinde boşluksuz, böylece genişlik tam orantılı.
            var inner = new GameObject("Inner", typeof(RectTransform)).GetComponent<RectTransform>();
            inner.SetParent(bg, false);
            Stretch(inner, 2f);
            ghost = Rect("Ghost", inner, ghostColor);
            Stretch(ghost, 0f);
            fill = Rect("Fill", inner, Color.white);
            Stretch(fill, 0f);
            fillGraphic = fill.GetComponent<Graphic>();
        }

        static RectTransform Rect(string n, Transform parent, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float pad)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }
    }
}
