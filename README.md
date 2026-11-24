# Health & Damage

**Health and damage system for Unity 6**: damage types, resistances, armor, invulnerability frames,
regeneration, teams, hitboxes (headshots), contact / projectile / impact damage, explosions,
world-space health bars, death and revive.

[English](#english) · [Türkçe](#türkçe)

---

<a name="english"></a>
# English

- **Health** component: max/current health, damage, heal, kill, revive, change max health.
- **Damage types** as assets (Fire, Poison…) and per-object **resistances** (0 = immune, 0.5 = resistant,
  2 = weak).
- **Armor** (flat reduction with a minimum), a global **damage multiplier**, **i-frames** after a hit,
  **god mode**, **regeneration** after a delay, **teams** with optional friendly fire.
- **BeforeDamage** hook for shields, blocking, parries, crit bonuses.
- **Hitbox**: put it on a collider (head, legs) to forward damage with a multiplier and a crit flag.
- **DamageOnContact**: spikes, lava (repeat while inside), bullets (destroy on hit), swords,
  fall/impact damage scaled by speed, knockback.
- **Damage.Explosion**: area damage with falloff, one hit per character, optional physics force and
  wall check.
- **HealthBar**: builds its own world-space bar above a character (with a "ghost" chunk that shows how
  much was lost), or drives your own HUD bar.
- Every result tells you **what happened**: how much was applied, whether it killed, and if it was
  blocked, **why** (dead, invulnerable, immune, friendly fire…).
- Inspector shows a live health bar with **Damage / Heal / Kill / Revive** buttons in Play mode.

**Requirements:** Unity 6 (6000.0+), uGUI (`com.unity.ugui`, installed by default).

## Step-by-step setup

### 1. Add the package

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/Health.git
```

To lock a version: `https://github.com/Atthree/Health.git#v0.1.0`

### 2. Try the demo

1. **Window > Package Manager** → select **Health & Damage** → **Samples** tab → **Demo** → **Import**.
2. **File > New Scene** (any template).
3. **GameObject > Create Empty**, then **Add Component > Health Demo**.
4. Press **Play**.

A turret shoots bullets and fireballs at five targets: normal, armored, fire resistant, i-frames, and
regenerating. A random 30% of shots aim at the head (Hitbox, 2× crit). Every 6 seconds an explosion
goes off. Dead targets come back after 3 seconds. Every hit and death is written to the Console.
Select a target to see its live health and the debug buttons.

### 3. Give something health

1. Select your character / enemy / crate → **Add Component > Atthree > Combat > Health**.
2. Set **Max Health**. Leave the rest at the defaults for now.
3. Optional: **Add Component > Atthree > Combat > Health Bar** for a bar above its head. Move it with
   **Offset** (the default is 2.2 m up, about right for a human-sized character).
4. Press **Play**, select the object, and click **Damage** in the Inspector.

### 4. Deal damage from code

```csharp
using Atthree.Combat;

// Raycast weapon
if (Physics.Raycast(ray, out var hit, 100f))
{
    Damage.Apply(hit.collider, new DamageInfo(25f, null, gameObject)
    {
        point = hit.point,
        direction = ray.direction,
    });
}

// Directly, when you already have the Health
enemyHealth.TakeDamage(10f);

// Explosion: 80 fire damage, 5 m radius, pushes rigidbodies
Damage.Explosion(transform.position, 5f, new DamageInfo(80f, fireType, gameObject), force: 15f);
```

`Damage.Apply` looks for a **Hitbox** on the collider first, then for a **Health** on the collider's
parents, so it works with characters whose colliders sit on child bones.

### 5. Damage without code

**Add Component > Atthree > Combat > Damage On Contact** on:

| Thing | Settings |
|---|---|
| Spikes | **Damage** 20, collider normal or trigger |
| Lava / poison cloud | Trigger collider, **Repeat Interval** 0.5 |
| Bullet / arrow | Trigger collider + Rigidbody, **Destroy On Hit** on, also **Destroy On Any Contact** to vanish on walls |
| Sword (child of the character) | Trigger collider, **Ignore Own Hierarchy** on (default), enable the collider only during the swing |
| Falling rock / car | Normal collider + Rigidbody, **Scale With Impact Speed** on |

Physics messages only arrive when at least one of the two objects has a **Rigidbody** (kinematic is
fine).

### 6. React to damage and death

In the Inspector: **On Damaged**, **On Healed**, **On Health Changed** (0..1), **On Died**,
**On Revived** are UnityEvents.

In code:

```csharp
health.Damaged += r => Debug.Log($"-{r.applied} from {r.info.source}");
health.DamageBlocked += r => { if (r.blocked == DamageBlock.Immune) ShowText("IMMUNE"); };
health.Died += r => animator.SetTrigger("Die");

// Any Health in the scene (score, damage numbers, screen shake, kill feed)
Health.AnyDamaged += r => ShowDamageNumber(r.info.point, r.applied, r.info.critical);
Health.AnyDied += r => score += 100;
```

**On Death** (Inspector) can also **Deactivate** or **Destroy** the object, with a delay for the death
animation.

## Damage pipeline

Every hit goes through these steps in order. The first one that stops it decides the `blocked` reason:

1. Component disabled → `Disabled`. Already dead → `Dead`.
2. Same team and friendly fire off → `FriendlyFire`.
3. God mode or inside i-frames (unless `ignoreInvulnerability`) → `Invulnerable`.
4. **BeforeDamage** handlers may change the info (shield absorbs, block halves). Reduced to 0 →
   `Modified`.
5. `amount × damageMultiplier × resistance(type)`. Result 0 → `Immune`.
6. Armor: `damage − armor`, but not below **Minimum Damage** (unless `ignoreArmor`). Result 0 →
   `Immune`.
7. Health goes down, i-frames start, the regeneration timer resets, events fire. 0 health → death.

Example: 40 fire damage, damage multiplier 1, fire resistance 0.5, armor 5 → 40 × 0.5 = 20 → 20 − 5 =
**15**.

## Recipes

**Shield that absorbs damage first:**

```csharp
float shield = 50f;
health.BeforeDamage += (Health h, ref DamageInfo info) =>
{
    float absorbed = Mathf.Min(shield, info.amount);
    shield -= absorbed;
    info.amount -= absorbed;
};
```

**Blocking from the front halves damage:**

```csharp
health.BeforeDamage += (Health h, ref DamageInfo info) =>
{
    if (isBlocking && Vector3.Dot(info.direction, -h.transform.forward) > 0.5f) info.amount *= 0.5f;
};
```

**Dash invulnerability:** `health.SetInvulnerableFor(0.3f);`

**Headshots:** add a separate collider on the head bone → **Add Component > Hitbox** → **Multiplier**
2, **Critical** on. The Health stays on the root.

**Level up:** `health.SetMaxHealth(health.Max + 20f, keepRatio: true);`

**Saving:** store `health.Current`, load with `health.SetHealth(value)`.

**HUD bar for the player:** build a UI Image bar (background + fill child with anchors stretched,
min 0,0 max 1,1), add **Health Bar** to it, turn **Auto Build** off, assign **Health** and **Fill**
(and optionally **Ghost**). Add a **CanvasGroup** if you want **Hide When Dead / Full** to work.

## Where to use it

| Game type | Typical setup |
|---|---|
| Action / hack-and-slash | Hitboxes on bones, sword DamageOnContact enabled during swings, i-frames on the player, world bars on enemies |
| Shooter | Raycast + `Damage.Apply`, headshot Hitbox, explosions, armor |
| Platformer | Small max health (3), i-frames 1–2 s, spikes and enemies with DamageOnContact |
| RPG | Damage types + resistances, regen, SetMaxHealth on level up |
| Physics / destruction | Crates with Health and DeathAction Destroy, impact damage scaled by speed |
| Tower defense / RTS | Teams, world bars with Hide When Full, `Health.AnyDied` for rewards |

## Good to know

- **Damage numbers and juice:** subscribe to `Health.AnyDamaged` and call your effects there (for
  example the **Game Feel** package's floating text, hit stop and shake). The Health package itself has
  no dependency on them.
- **Event Bus:** if you use the **Event Bus** package, publish your own events from `Health.AnyDied`
  in one bridge script instead of referencing Health everywhere.
- **Time scale:** i-frames and regen use scaled time, so they pause with your game.
- **Minimum Damage** defaults to 1: with armor, every hit does at least 1. Set it to 0 if armor should
  be able to block hits completely.
- **Teams:** `-1` means "no team". A Health with no team takes damage from everyone; damage with no team
  hits everyone.
- **Kill** ignores armor, resistances and i-frames. **Revive** only works on a dead Health.
- **Heal** does nothing on a dead Health; use **Revive**.
- **DeathAction.Deactivate/Destroy** happen after the death events, so your listeners still see the
  object.

## Using it with your own models

- **Simple objects** (crate, barrel, door): Health on the object with the collider. That's it.
- **Characters with a single capsule** (CharacterController): Health on the root. If you want headshots,
  add a sphere collider (trigger is fine for raycasts with *Queries Hit Triggers* on) on the head bone
  with a Hitbox.
- **Ragdolls / bone colliders** (for example the **Active Ragdoll** package): Health on the root,
  **Hitbox** on each bone collider you care about (head ×2, torso ×1, limbs ×0.75). Explosions count
  each character once, no matter how many bone colliders are in range.
- **Health bar height:** set **Offset Y** to slightly above the model's head. For very large enemies,
  increase **Size**.
- **Prefabs:** add Health/HealthBar to the prefab, not the instance, so every copy has them.

## Troubleshooting

| Problem | Cause and fix |
|---|---|
| DamageOnContact never hits | Neither object has a Rigidbody, the layer is not in **Hit Layers**, or the target has no Health/Hitbox in its parents. |
| Sword hurts its own wielder | The sword is not a child of the character. Keep it in the character's hierarchy, or set **Team** on both. |
| Hits are ignored | Check the `blocked` reason: `Invulnerable` (i-frames too long?), `Immune` (resistance 0 or armor too high with Minimum Damage 0), `FriendlyFire`. |
| Health bar not visible | No **Main Camera** tag on the camera (billboard), the offset is inside the model, or **Hide When Full** is on. |
| Health bar too big / small | Change **Size** (in meters). |
| Explosion damages through walls | Pass `obstacleMask` (your wall layers) to `Damage.Explosion`. |

## How it works

- `IDamageable` is the only contract: `DamageResult TakeDamage(DamageInfo info)`. Health and Hitbox
  implement it; your own classes can too.
- `DamageInfo` is a struct carrying amount, type, source, point, direction, crit, team and two bypass
  flags. `DamageResult` returns incoming vs. applied damage, whether it killed and the block reason.
- The world health bar is a world-space Canvas built at runtime on its own root object, so it does not
  rotate with the character, and it is destroyed with the Health. Bar width uses `anchorMax.x`, so it
  works with any sprite or none.
- Explosions use `Physics.OverlapSphereNonAlloc`, keep the closest collider per Health, scale by
  distance to the collider's closest point, and push each Rigidbody once.
- Static events are reset on Play (works with domain reload off).

## Tests

20 PlayMode tests: damage and overkill, armor and minimum damage, resistances, i-frames, friendly fire,
shield via BeforeDamage, heal and revive, max health changes, regeneration, static events, death action,
Hitbox, `Damage.Apply`, explosions with falloff and one hit per Health, projectile and repeat contact
damage, own-hierarchy ignore, and the auto-built health bar.

To run them, add to `Packages/manifest.json`:

```json
"testables": [ "com.atthree.health" ]
```

then **Window > General > Test Runner > PlayMode > Run All**.

## License

[MIT](LICENSE.md)

---

<a name="türkçe"></a>
# Türkçe

- **Health** bileşeni: max/mevcut can, hasar, iyileşme, öldürme, diriltme, max canı değiştirme.
- Asset olarak **hasar türleri** (Ateş, Zehir…) ve obje başına **dirençler** (0 = bağışık,
  0.5 = dirençli, 2 = zayıf).
- **Zırh** (minimumlu düz azaltma), genel **hasar çarpanı**, vuruştan sonra **dokunulmazlık (i-frame)**,
  **god mode**, gecikmeli **can yenilenmesi**, isteğe bağlı dost ateşiyle **takımlar**.
- Kalkan, blok, parry, kritik bonusu için **BeforeDamage** kancası.
- **Hitbox**: bir collider'a (kafa, bacak) ekle, hasarı çarpan ve kritik bayrağıyla iletsin.
- **DamageOnContact**: diken, lav (içindeyken tekrar), mermi (vurunca yok ol), kılıç, hıza göre
  düşme/çarpma hasarı, geri itme.
- **Damage.Explosion**: mesafeyle azalan alan hasarı, karakter başına tek vuruş, isteğe bağlı fizik
  itmesi ve duvar kontrolü.
- **HealthBar**: karakterin üstüne kendi dünya-uzayı barını kurar (ne kadar can gittiğini gösteren
  "ghost" parçasıyla), ya da kendi HUD barını sürer.
- Her sonuç **ne olduğunu** söyler: ne kadar hasar gitti, öldürdü mü, engellendiyse **neden**
  (ölü, dokunulmaz, bağışık, dost ateşi…).
- Play modunda Inspector canlı can barı ve **Damage / Heal / Kill / Revive** düğmeleri gösterir.

**Gereksinimler:** Unity 6 (6000.0+), uGUI (`com.unity.ugui`, varsayılan olarak kurulu).

## Adım adım kurulum

### 1. Paketi ekle

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/Health.git
```

Belirli bir sürüm için: `https://github.com/Atthree/Health.git#v0.1.0`

### 2. Demoyu dene

1. **Window > Package Manager** → **Health & Damage**'ı seç → **Samples** sekmesi → **Demo** → **Import**.
2. **File > New Scene** (herhangi bir şablon).
3. **GameObject > Create Empty**, sonra **Add Component > Health Demo**.
4. **Play**'e bas.

Bir taret beş hedefe mermi ve ateş topu atar: normal, zırhlı, ateşe dirençli, i-frame'li ve kendini
yenileyen. Atışların rastgele %30'u kafaya gider (Hitbox, 2× kritik). Her 6 saniyede bir patlama olur.
Ölen hedefler 3 saniye sonra dirilir. Her vuruş ve ölüm Console'a yazılır. Bir hedefi seçince canlı
canını ve test düğmelerini görürsün.

### 3. Bir şeye can ver

1. Karakterini / düşmanını / sandığını seç → **Add Component > Atthree > Combat > Health**.
2. **Max Health**'i ayarla. Gerisini şimdilik varsayılan bırak.
3. İsteğe bağlı: kafasının üstünde bar için **Add Component > Atthree > Combat > Health Bar**.
   Yüksekliği **Offset** ile ayarla (varsayılan 2.2 m, insan boyu karakter için uygun).
4. **Play**'e bas, objeyi seç, Inspector'da **Damage**'a tıkla.

### 4. Koddan hasar ver

```csharp
using Atthree.Combat;

// Raycast silah
if (Physics.Raycast(ray, out var hit, 100f))
{
    Damage.Apply(hit.collider, new DamageInfo(25f, null, gameObject)
    {
        point = hit.point,
        direction = ray.direction,
    });
}

// Health elindeyse doğrudan
enemyHealth.TakeDamage(10f);

// Patlama: 80 ateş hasarı, 5 m yarıçap, rigidbody'leri iter
Damage.Explosion(transform.position, 5f, new DamageInfo(80f, fireType, gameObject), force: 15f);
```

`Damage.Apply` önce collider'daki **Hitbox**'a, sonra collider'ın üst objelerindeki **Health**'e bakar;
collider'ları alt kemiklerde olan karakterlerde de çalışır.

### 5. Kodsuz hasar

**Add Component > Atthree > Combat > Damage On Contact** şunlara:

| Şey | Ayar |
|---|---|
| Diken | **Damage** 20, collider normal ya da trigger |
| Lav / zehir bulutu | Trigger collider, **Repeat Interval** 0.5 |
| Mermi / ok | Trigger collider + Rigidbody, **Destroy On Hit** açık; duvarda da yok olsun diye **Destroy On Any Contact** |
| Kılıç (karakterin çocuğu) | Trigger collider, **Ignore Own Hierarchy** açık (varsayılan), collider'ı sadece savuruş sırasında aç |
| Düşen kaya / araba | Normal collider + Rigidbody, **Scale With Impact Speed** açık |

Fizik mesajları ancak iki objeden en az birinde **Rigidbody** varsa gelir (kinematic de olur).

### 6. Hasara ve ölüme tepki ver

Inspector'da: **On Damaged**, **On Healed**, **On Health Changed** (0..1), **On Died**, **On Revived**
UnityEvent'leri.

Kodda:

```csharp
health.Damaged += r => Debug.Log($"-{r.applied}, vuran: {r.info.source}");
health.DamageBlocked += r => { if (r.blocked == DamageBlock.Immune) ShowText("BAĞIŞIK"); };
health.Died += r => animator.SetTrigger("Die");

// Sahnedeki herhangi bir Health (skor, hasar sayıları, ekran sarsıntısı, öldürme listesi)
Health.AnyDamaged += r => ShowDamageNumber(r.info.point, r.applied, r.info.critical);
Health.AnyDied += r => score += 100;
```

Inspector'daki **On Death** objeyi ölüm animasyonu için gecikmeli olarak **Deactivate** ya da
**Destroy** da edebilir.

## Hasar sırası

Her vuruş bu adımlardan sırayla geçer. Onu durduran ilk adım `blocked` sebebini belirler:

1. Bileşen kapalı → `Disabled`. Zaten ölü → `Dead`.
2. Aynı takım ve dost ateşi kapalı → `FriendlyFire`.
3. God mode ya da i-frame içinde (`ignoreInvulnerability` yoksa) → `Invulnerable`.
4. **BeforeDamage** dinleyicileri bilgiyi değiştirebilir (kalkan emer, blok yarıya indirir). 0'a inerse →
   `Modified`.
5. `miktar × damageMultiplier × direnç(tür)`. Sonuç 0 → `Immune`.
6. Zırh: `hasar − zırh`, ama **Minimum Damage**'ın altına inmez (`ignoreArmor` yoksa). Sonuç 0 → `Immune`.
7. Can düşer, i-frame başlar, yenilenme sayacı sıfırlanır, olaylar çalışır. Can 0 → ölüm.

Örnek: 40 ateş hasarı, çarpan 1, ateş direnci 0.5, zırh 5 → 40 × 0.5 = 20 → 20 − 5 = **15**.

## Tarifler

**Önce hasarı emen kalkan:**

```csharp
float shield = 50f;
health.BeforeDamage += (Health h, ref DamageInfo info) =>
{
    float absorbed = Mathf.Min(shield, info.amount);
    shield -= absorbed;
    info.amount -= absorbed;
};
```

**Önden gelen hasarı yarıya indiren blok:**

```csharp
health.BeforeDamage += (Health h, ref DamageInfo info) =>
{
    if (isBlocking && Vector3.Dot(info.direction, -h.transform.forward) > 0.5f) info.amount *= 0.5f;
};
```

**Dash dokunulmazlığı:** `health.SetInvulnerableFor(0.3f);`

**Kafa vuruşu:** kafa kemiğine ayrı bir collider ekle → **Add Component > Hitbox** → **Multiplier** 2,
**Critical** açık. Health kökte kalır.

**Seviye atlama:** `health.SetMaxHealth(health.Max + 20f, keepRatio: true);`

**Kayıt:** `health.Current`'ı kaydet, `health.SetHealth(value)` ile yükle.

**Oyuncu için HUD barı:** UI Image ile bir bar kur (arka plan + anchor'ları gerilmiş dolgu çocuğu,
min 0,0 max 1,1), ona **Health Bar** ekle, **Auto Build**'i kapat, **Health** ve **Fill**'i ata
(istersen **Ghost**'u da). **Hide When Dead / Full** çalışsın istiyorsan bir **CanvasGroup** ekle.

## Nerede kullanılır?

| Oyun türü | Tipik kurulum |
|---|---|
| Aksiyon / hack-and-slash | Kemiklerde Hitbox, savuruşta açılan kılıç DamageOnContact'ı, oyuncuda i-frame, düşmanlarda dünya barı |
| Nişancı | Raycast + `Damage.Apply`, kafa Hitbox'ı, patlamalar, zırh |
| Platform | Küçük max can (3), 1–2 sn i-frame, DamageOnContact'lı diken ve düşmanlar |
| RPG | Hasar türleri + dirençler, yenilenme, seviye atlayınca SetMaxHealth |
| Fizik / yıkım | Health + DeathAction Destroy'lu sandıklar, hıza göre çarpma hasarı |
| Kule savunma / RTS | Takımlar, Hide When Full'lu dünya barları, ödül için `Health.AnyDied` |

## Dikkat edilecekler

- **Hasar sayıları ve juice:** `Health.AnyDamaged`'e abone ol, efektlerini orada çağır (örneğin
  **Game Feel** paketinin floating text, hit stop ve sarsıntısı). Health paketi onlara bağımlı değil.
- **Event Bus:** **Event Bus** paketini kullanıyorsan, her yerde Health'e referans vermek yerine tek bir
  köprü script'inde `Health.AnyDied`'dan kendi olaylarını yayınla.
- **Time scale:** i-frame ve yenilenme ölçekli zaman kullanır, oyun durunca onlar da durur.
- **Minimum Damage** varsayılan 1: zırh varken her vuruş en az 1 vurur. Zırh vuruşları tamamen
  engelleyebilsin istiyorsan 0 yap.
- **Takımlar:** `-1` "takımsız" demek. Takımsız Health herkesten hasar alır; takımsız hasar herkese işler.
- **Kill** zırh, direnç ve i-frame'i yok sayar. **Revive** sadece ölü Health'te çalışır.
- **Heal** ölü Health'te bir şey yapmaz; **Revive** kullan.
- **DeathAction.Deactivate/Destroy** ölüm olaylarından sonra olur, dinleyicilerin objeyi hâlâ görür.

## Kendi modellerinle

- **Basit objeler** (sandık, varil, kapı): collider'ın olduğu objeye Health. Bu kadar.
- **Tek kapsüllü karakterler** (CharacterController): Health kökte. Kafa vuruşu istiyorsan kafa kemiğine
  Hitbox'lı bir küre collider ekle (raycast'ler *Queries Hit Triggers* açıksa trigger da olur).
- **Ragdoll / kemik collider'ları** (örneğin **Active Ragdoll** paketi): Health kökte, önemsediğin her
  kemik collider'ına **Hitbox** (kafa ×2, gövde ×1, kollar/bacaklar ×0.75). Patlamalar, menzildeki
  kemik collider sayısından bağımsız olarak her karakteri bir kez sayar.
- **Can barı yüksekliği:** **Offset Y**'yi modelin kafasının biraz üstüne ayarla. Çok büyük düşmanlar
  için **Size**'ı büyüt.
- **Prefab'lar:** Health/HealthBar'ı sahnedeki kopyaya değil prefab'ın kendisine ekle, her kopyada olsun.

## Sorun giderme

| Sorun | Sebep ve çözüm |
|---|---|
| DamageOnContact hiç vurmuyor | İki objede de Rigidbody yok, katman **Hit Layers**'ta değil ya da hedefin üstlerinde Health/Hitbox yok. |
| Kılıç sahibine vuruyor | Kılıç karakterin çocuğu değil. Karakterin hiyerarşisinde tut ya da ikisine de **Team** ver. |
| Vuruşlar yok sayılıyor | `blocked` sebebine bak: `Invulnerable` (i-frame çok mu uzun?), `Immune` (direnç 0 ya da Minimum Damage 0 iken zırh çok yüksek), `FriendlyFire`. |
| Can barı görünmüyor | Kamerada **MainCamera** tag'i yok (billboard), offset modelin içinde ya da **Hide When Full** açık. |
| Can barı çok büyük / küçük | **Size**'ı değiştir (metre cinsinden). |
| Patlama duvarın arkasına vuruyor | `Damage.Explosion`'a `obstacleMask` (duvar katmanların) ver. |

## Nasıl çalışıyor?

- Tek sözleşme `IDamageable`: `DamageResult TakeDamage(DamageInfo info)`. Health ve Hitbox bunu
  uygular; kendi sınıfların da uygulayabilir.
- `DamageInfo` miktar, tür, kaynak, nokta, yön, kritik, takım ve iki atlama bayrağı taşıyan bir struct.
  `DamageResult` gelen ve uygulanan hasarı, öldürüp öldürmediğini ve engel sebebini döndürür.
- Dünya can barı, çalışma anında kendi kök objesinde kurulan bir world-space Canvas'tır; böylece
  karakterle birlikte dönmez ve Health ile birlikte silinir. Bar genişliği `anchorMax.x` ile ayarlanır,
  herhangi bir sprite ile (ya da sprite'sız) çalışır.
- Patlamalar `Physics.OverlapSphereNonAlloc` kullanır, her Health için en yakın collider'ı tutar,
  collider'ın en yakın noktasına olan mesafeye göre ölçekler, her Rigidbody'yi bir kez iter.
- Statik olaylar Play başında sıfırlanır (domain reload kapalıyken de çalışır).

## Testler

20 PlayMode testi. Çalıştırmak için `Packages/manifest.json`'a ekle:

```json
"testables": [ "com.atthree.health" ]
```

sonra **Window > General > Test Runner > PlayMode > Run All**.

## Lisans

[MIT](LICENSE.md)
