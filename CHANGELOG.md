# Changelog

## [0.1.0] - 2026-11-24
- Initial version: `Health` (damage, heal, kill, revive, max health, armor with minimum damage, damage
  multiplier, resistances per `DamageType`, i-frames, god mode, teams/friendly fire, regeneration,
  death actions, BeforeDamage modifiers, C# + UnityEvents, static AnyDamaged/AnyDied).
- `DamageInfo` / `DamageResult` with block reasons, `IDamageable`, `Hitbox` (multiplier, crit),
  `Damage.Apply` / `Damage.Explosion` (falloff, one hit per Health, force, obstacle check),
  `DamageOnContact` (trigger/collision, repeat, projectile, impact speed, knockback), `HealthBar`
  (auto-built world bar with ghost chunk, or HUD mode).
- Editor: Health inspector with live bar and debug buttons. Demo sample.
- 20 PlayMode tests.
