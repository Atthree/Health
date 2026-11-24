using UnityEditor;
using UnityEngine;

namespace Atthree.Combat.Editor
{
    /// <summary>Health Inspector'ına canlı can barı ve Play modunda test düğmeleri ekler.</summary>
    [CustomEditor(typeof(Health))]
    [CanEditMultipleObjects]
    public class HealthEditor : UnityEditor.Editor
    {
        float amount = 10f;

        public override void OnInspectorGUI()
        {
            var h = (Health)target;

            if (Application.isPlaying)
            {
                var rect = EditorGUILayout.GetControlRect(false, 20f);
                string label = h.IsDead ? "ÖLÜ" : $"{h.Current:0.#} / {h.Max:0.#}" + (h.IsInvulnerable ? "  (dokunulmaz)" : "");
                EditorGUI.ProgressBar(rect, h.Normalized, label);

                using (new EditorGUILayout.HorizontalScope())
                {
                    amount = EditorGUILayout.FloatField(amount, GUILayout.Width(60));
                    if (GUILayout.Button("Damage")) foreach (Health t in targets) t.TakeDamage(new DamageInfo(amount) { ignoreInvulnerability = true });
                    if (GUILayout.Button("Heal")) foreach (Health t in targets) t.Heal(amount);
                    if (GUILayout.Button("Kill")) foreach (Health t in targets) t.Kill();
                    if (GUILayout.Button("Revive")) foreach (Health t in targets) t.Revive();
                }
                EditorGUILayout.Space();
            }

            DrawDefaultInspector();
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
