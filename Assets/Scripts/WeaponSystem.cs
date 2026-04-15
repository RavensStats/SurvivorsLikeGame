using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WeaponSystem : MonoBehaviour {
    public static WeaponSystem Instance;
    public List<ItemData> activeWeapons, passiveItems;
    public List<ItemData> cardPool = new List<ItemData>();
    public List<EvolutionRecipe> recipes;
    private Dictionary<string, float> cooldowns = new Dictionary<string, float>();
    private Dictionary<string, List<GameObject>> activeOrbs = new Dictionary<string, List<GameObject>>();

    // Always use the live player position so projectiles spawn/query at the right spot.
    Vector3 PlayerPos => SurvivorMasterScript.Instance.player?.position ?? transform.position;

    void Awake() => Instance = this;

    // Resets all run-time weapon state to defaults so a new run starts clean.
    public void ResetForNewRun() {
        // Reload the full card pool first (evolution may have removed items like Flame).
        GetComponent<ZenithDatabaseLoader>()?.ReloadDatabase();
        // Reset levels on all pooled items back to 1.
        foreach (var item in cardPool) item.level = 1;
        activeWeapons.Clear();
        passiveItems.Clear();
        cooldowns.Clear();
        // Destroy any active orbs.
        foreach (var kvp in activeOrbs)
            foreach (var o in kvp.Value)
                if (o != null) Destroy(o);
        activeOrbs.Clear();
    }

    void Update() {
        if (Time.timeScale <= 0) return;

        // Berserker passive: cooldowns shrink as player HP drops (up to 50% faster at 1 HP).
        float berserkerMult = 1f;
        if (SurvivorMasterScript.Instance != null &&
            SurvivorMasterScript.Instance.currentClass == CharacterClass.Berserker) {
            float hpRatio = SurvivorMasterScript.Instance.HPRatio;
            berserkerMult = Mathf.Lerp(0.5f, 1.0f, hpRatio); // 1.0 at full HP, 0.5 at 0 HP
        }

        // Tick disable timer
        if (_disableTimer > 0) _disableTimer -= Time.deltaTime;
        else _disabledWeapon = null;

        if (_frozen) return; // Chrono freeze — skip all firing

        float poiCd = SurvivorMasterScript.Instance?.poiCooldownMult ?? 1f;

        foreach (var w in activeWeapons) {
            if (w.itemName == _disabledWeapon) continue; // Siren disable
            if (!cooldowns.ContainsKey(w.itemName)) cooldowns[w.itemName] = 0;
            cooldowns[w.itemName] -= Time.deltaTime / (PersistentUpgrades.CooldownMult * berserkerMult * poiCd);
            if (cooldowns[w.itemName] <= 0) { Fire(w); cooldowns[w.itemName] = w.cooldown; }
        }
    }

    void Fire(ItemData w) {
        // Non-projectile modes are handled first (no prefab needed)
        if (w.fireMode == FireMode.ArcSwing)   { FireArcSwing(w);   return; }
        if (w.fireMode == FireMode.Orbit)       { FireOrbit(w);      return; }
        if (w.fireMode == FireMode.ScytheOrbit) { StartCoroutine(FireScytheSwipe(w)); return; }
        if (w.fireMode == FireMode.RisingFist)  { StartCoroutine(FireRisingFist(w)); return; }

        if (w.projectilePrefab == null && string.IsNullOrEmpty(w.spriteFolder)) { Debug.LogWarning($"[WeaponSystem] '{w.itemName}' has no projectilePrefab or spriteFolder assigned."); return; }
        switch (w.fireMode) {
            case FireMode.NearestN:      FireNearestN(w);      break;
            case FireMode.RandomInRange: FireRandomInRange(w); break;
            default:                     FireDefault(w);       break;
        }
    }

    // Fixed attack directions per level: Lv1=E, Lv2=E+W, Lv3=E+W+N, Lv4=E+W+N+S, Lv5=all 8.
    static readonly Vector2[][] SwordDirectionsByLevel = {
        new[] { Vector2.right },
        new[] { Vector2.right, Vector2.left },
        new[] { Vector2.right, Vector2.left, Vector2.up },
        new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down },
        new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                new Vector2(1,1).normalized,  new Vector2(-1,1).normalized,
                new Vector2(1,-1).normalized, new Vector2(-1,-1).normalized }
    };

    // Deals damage to all enemies within a 120° arc in level-based fixed directions.
    void FireArcSwing(ItemData w) {
        float range = (w.range > 0f ? w.range : 3f) * 0.8f;  // 20% less range
        const float halfAngle = 60f;  // 120° total arc per direction
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        targets = targets.Distinct().ToList();

        int levelIdx = Mathf.Clamp(w.level, 1, 5) - 1;
        Vector2[] directions = SwordDirectionsByLevel[levelIdx];
        float rangeSq = range * range;
        var alreadyHit = new HashSet<EnemyEntity>();

        foreach (Vector2 swingDir in directions) {
            foreach (var e in targets) {
                if (alreadyHit.Contains(e)) continue;
                Vector2 toEnemy = (Vector2)(e.transform.position - pos);
                if (toEnemy.sqrMagnitude > rangeSq) continue;
                if (Vector2.Angle(swingDir, toEnemy) <= halfAngle) {
                    float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
                    e.TakeDamage(dmg);
                    alreadyHit.Add(e);
                }
            }
            if (!string.IsNullOrEmpty(w.spriteFolder)) {
                Sprite spr = w.spriteFolder.Contains("/")
                    ? Resources.Load<Sprite>($"Sprites/{w.spriteFolder}")
                    : Resources.Load<Sprite>($"Sprites/Weapons/{w.spriteFolder}/{w.spriteFolder}");
                if (spr != null) StartCoroutine(SwordSlide(spr, pos, swingDir, range));
            }
        }
    }

    IEnumerator SwordSlide(Sprite spr, Vector3 origin, Vector2 dir, float range) {
        var vfx = new GameObject("SwingVFX_Sword");
        var sr = vfx.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sortingOrder = 7;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        vfx.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        // Scale the sprite so its length equals the weapon range, then shrink by 50%
        float naturalWidth = spr.rect.width / spr.pixelsPerUnit;
        float s = (naturalWidth > 0f ? range / naturalWidth : 1f) * 0.5f;
        vfx.transform.localScale = new Vector3(s, s, 1f);
        var col = vfx.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        const float duration = 0.5f;
        float t = 0f;
        while (t < duration) {
            if (vfx == null) yield break;
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            // Slide from origin outward to 60% of range (40% closer to player)
            vfx.transform.position = origin + (Vector3)(dir * range * 0.6f * progress);
            // Fade in: transparent at center, fully opaque at full extension
            sr.color = new Color(1f, 1f, 1f, progress);
            yield return null;
        }
        if (vfx != null) Destroy(vfx);
    }

    // Immediately rebuilds the orbs for an Orbit weapon so its sprite/count
    // reflects a just-applied upgrade without waiting for the next cooldown tick.
    // ScytheOrbit is a one-shot swipe — no persistent objects to refresh.
    public void RefreshOrbitWeapon(ItemData w) {
        if (w.fireMode == FireMode.ScytheOrbit) return; // nothing persistent to rebuild
        if (w.fireMode != FireMode.Orbit) return;
        if (activeOrbs.ContainsKey(w.itemName)) {
            foreach (var o in activeOrbs[w.itemName])
                if (o != null) Destroy(o);
            activeOrbs[w.itemName].Clear();
        }
        FireOrbit(w);
    }

    // Spawns a scythe that sweeps one full clockwise rotation around the player then disappears.
    // Scale, cooldown, and damage all increase substantially with level.
    IEnumerator FireScytheSwipe(ItemData w) {
        // Level-scaled cooldown (L1=5s → L5=1.5s, evenly spaced) — must be set before first yield.
        w.cooldown = Mathf.Lerp(5f, 1.5f, (w.level - 1) / 4f);

        // Level-scaled sprite scale (L1=2.5 → L5=4.0, evenly spaced).
        float sprScale  = Mathf.Lerp(5f, 8f, (w.level - 1) / 4f);
        const float orbitRadius   = 4f;
        const float swipeDuration = 0.8f;            // fixed time for one full 360° sweep
        const float orbitSpeed    = 360f / swipeDuration; // deg/sec

        // Load sprite.
        Sprite spr = null;
        if (!string.IsNullOrEmpty(w.spriteFolder)) {
            spr = w.spriteFolder.Contains("/")
                ? Resources.Load<Sprite>($"Sprites/{w.spriteFolder}")
                : Resources.Load<Sprite>($"Sprites/Weapons/{w.spriteFolder}/{w.spriteFolder}");
        }

        var scythe = new GameObject("ScytheSwipe_VFX");
        var sr     = scythe.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 7;
        if (spr != null) { sr.sprite = spr; }
        else { sr.color = new Color(0.4f, 0f, 0.8f); }
        scythe.transform.localScale = Vector3.one * sprScale;

        float dmg = w.baseDamage * Mathf.Pow(2f, w.level - 1)
                  * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f)
                  * (1f + RunUpgrades.DamageBonus);

        var hitSet = new HashSet<EnemyEntity>(); // each enemy hit at most once per swipe
        float angle   = 0f;
        float elapsed = 0f;

        while (elapsed < swipeDuration) {
            if (scythe == null) yield break;
            var player = SurvivorMasterScript.Instance?.player;
            if (player == null) break;

            elapsed += Time.deltaTime;
            angle   -= orbitSpeed * Time.deltaTime; // negative = clockwise

            float rad    = angle * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
            scythe.transform.position = (Vector2)player.position + offset;
            // Bottom-left corner faces the player: sprite_Z = orbitAngle - 45°
            scythe.transform.rotation = Quaternion.Euler(0f, 0f, angle - 45f);

            // Damage check via physics overlap at scythe position.
            float hitRadius = sprScale * 0.5f;
            foreach (var col in Physics2D.OverlapCircleAll(scythe.transform.position, hitRadius)) {
                if (!col.CompareTag("Enemy")) continue;
                var e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead || hitSet.Contains(e)) continue;
                hitSet.Add(e);
                e.TakeDamage(dmg);
            }

            yield return null;
        }

        if (scythe != null) Destroy(scythe);
    }

    // Spawns/maintains N orbiting objects (N = weapon level) that deal periodic burn damage.
    void FireOrbit(ItemData w) {
        if (!activeOrbs.ContainsKey(w.itemName))
            activeOrbs[w.itemName] = new List<GameObject>();

        activeOrbs[w.itemName].RemoveAll(o => o == null);

        // Orbs already match the weapon level — nothing to do
        if (activeOrbs[w.itemName].Count == w.level) return;

        foreach (var o in activeOrbs[w.itemName])
            if (o != null) Destroy(o);
        activeOrbs[w.itemName].Clear();

        for (int i = 0; i < w.level; i++) {
            var orb = new GameObject($"Orb_{w.itemName}_{i}");
            var sr = orb.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 6;
            // Load sprite from the weapon's spriteFolder if set; fall back to tinted circle.
            if (!string.IsNullOrEmpty(w.spriteFolder)) {
                Sprite spr = w.spriteFolder.Contains("/")
                    ? Resources.Load<Sprite>($"Sprites/{w.spriteFolder}")
                    : Resources.Load<Sprite>($"Sprites/Weapons/{w.spriteFolder}/{w.spriteFolder}");
                if (spr != null) {
                    sr.sprite = spr;
                    orb.transform.localScale = Vector3.one * 4f;   // 400 % scale for sprite orbs
                } else {
                    sr.color  = new Color(1f, 0.45f, 0.05f);
                }
            } else {
                sr.color = new Color(1f, 0.45f, 0.05f);
            }
            var logic = orb.AddComponent<OrbLogic>();
            logic.startAngle = (360f / w.level) * i;
            logic.damage     = w.baseDamage;
            activeOrbs[w.itemName].Add(orb);
        }
    }

    // Creates a projectile from a prefab or, when none is assigned, builds one at runtime
    // using the sprite found at Resources/Sprites/Weapons/{w.spriteFolder}/.
    GameObject SpawnProjectile(ItemData w, Vector3 pos) {
        if (w.projectilePrefab != null)
            return Instantiate(w.projectilePrefab, pos, Quaternion.identity);
        var go = new GameObject($"Proj_{w.itemName}");
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<ProjectileLogic>();
        return go;
    }

    void FireDefault(ItemData w) {
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        Vector2 dir = targets.Count > 0 ? (targets[0].transform.position - pos).normalized : Random.insideUnitCircle.normalized;
        SpawnProjectile(w, pos).GetComponent<ProjectileLogic>().Setup(w, dir);
    }

    // Fires one projectile at each of the N nearest enemies (N = weapon level).
    void FireNearestN(ItemData w) {
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        // Deduplicate — SpatialGrid's 3x3 cell scan can include the same entity twice
        // if it sits on a shared boundary during an UpdateEntity call.
        targets = targets.Distinct().ToList();
        targets.Sort((a, b) =>
            (a.transform.position - pos).sqrMagnitude
            .CompareTo((b.transform.position - pos).sqrMagnitude));
        int count = Mathf.Min(w.level, targets.Count);
        if (count == 0) {
            SpawnProjectile(w, pos).GetComponent<ProjectileLogic>().Setup(w, Random.insideUnitCircle.normalized);
            return;
        }
        for (int i = 0; i < count; i++) {
            Vector2 dir = (targets[i].transform.position - pos).normalized;
            SpawnProjectile(w, pos).GetComponent<ProjectileLogic>().Setup(w, dir);
        }
    }

    // Fires one projectile at each of up to N random enemies within w.range (N = weapon level).
    void FireRandomInRange(ItemData w) {
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        float rangeSq = w.range * w.range;
        targets = targets.Where(e => (e.transform.position - pos).sqrMagnitude <= rangeSq).ToList();
        for (int i = targets.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            var tmp = targets[i]; targets[i] = targets[j]; targets[j] = tmp;
        }
        int count = Mathf.Min(w.level, targets.Count);
        for (int i = 0; i < count; i++) {
            Vector2 dir = (targets[i].transform.position - pos).normalized;
            SpawnProjectile(w, pos).GetComponent<ProjectileLogic>().Setup(w, dir, targets[i]);
        }
    }

    public void CheckSynergies() {
        // Tag Synergy Logic
        int fireCount = activeWeapons.Concat(passiveItems).Count(i => i.tags.Contains("Fire"));
        if (fireCount >= 3) StartCoroutine(InfernoAura());

        // Evolution Logic — swap weapon + passive into the evolved weapon
        foreach (var r in recipes) {
            ItemData weapon  = activeWeapons.Find(w => w.itemName == r.weaponA);
            ItemData passive = passiveItems.Find(p => p.itemName == r.itemB);
            if (weapon == null || passive == null) continue;

            // Avoid evolving the same combo twice
            if (activeWeapons.Exists(w => w.itemName == r.resultName)) continue;

            activeWeapons.Remove(weapon);
            passiveItems.Remove(passive);
            if (cooldowns.ContainsKey(weapon.itemName)) cooldowns.Remove(weapon.itemName);

            ItemData evolved = new ItemData {
                itemName    = r.resultName,
                description = $"Evolved form of {r.weaponA}.",
                isWeapon    = true,
                rarity      = Rarity.Legendary,
                baseDamage  = weapon.baseDamage * 2.5f,
                cooldown    = weapon.cooldown * 0.7f,
                pierceCount = weapon.pierceCount + 2,
                trait       = weapon.trait,
                tags        = new System.Collections.Generic.List<string>(weapon.tags),
                projectilePrefab = r.evolvedPrefab != null ? r.evolvedPrefab : weapon.projectilePrefab
            };
            activeWeapons.Add(evolved);
            cardPool.RemoveAll(c => c.itemName == r.weaponA || c.itemName == r.itemB);
        }
    }

    // Shared scratch list – reused across InfernoAura ticks to avoid per-tick allocation.
    private readonly List<EnemyEntity> _infernoScratch = new List<EnemyEntity>();

    IEnumerator InfernoAura() {
        float duration = 5f;
        float tickInterval = 0.5f;
        float damagePerTick = 8f;
        float elapsed = 0f;
        while (elapsed < duration) {
            SurvivorMasterScript.Instance.Grid.GetNearby(PlayerPos, _infernoScratch);
            foreach (var e in _infernoScratch)
                if (e != null) e.TakeDamage(damagePerTick);
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }

    // ── Enemy-inflicted debuffs ──────────────────────────────────────────────
    private bool _frozen;
    public void SetFrozen(bool v) => _frozen = v;

    private string _disabledWeapon;
    private float  _disableTimer;
    public void DisableRandomWeapon(float duration) {
        if (activeWeapons.Count == 0) return;
        _disabledWeapon = activeWeapons[Random.Range(0, activeWeapons.Count)].itemName;
        _disableTimer   = duration;
    }

    // Spawns a giant fist sprite aimed at the nearest enemy and drives it forward,
    // damaging and knocking back every enemy it overlaps along the way.
    // Scale and knockback grow with weapon level.
    IEnumerator FireRisingFist(ItemData w) {
        Vector3 playerPos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(playerPos);
        targets.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        targets = targets.Distinct().ToList();
        if (targets.Count == 0) yield break;

        // Pick the nearest enemy as the strike target position
        targets.Sort((a, b) =>
            (a.transform.position - playerPos).sqrMagnitude
            .CompareTo((b.transform.position - playerPos).sqrMagnitude));
        Vector3 strikePos = targets[0].transform.position;

        // Build the fist visual
        Sprite fistSprite = Resources.Load<Sprite>($"Sprites/Weapons/{w.spriteFolder}/{w.spriteFolder}");
        var fist = new GameObject("RisingFist_VFX");
        var sr   = fist.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        if (fistSprite != null) {
            sr.sprite = fistSprite;
            // Scale: 1.5× at level 1, growing by 0.5× per level (3.5× at level 5)
            float scale = w.projectileScale * (1.5f + (w.level - 1) * 0.5f);
            fist.transform.localScale = Vector3.one * scale;
        }

        // 45° diagonal rise: bottom-left → top-right
        float riseHeight   = 6f;
        float riseDuration = 1.0f;
        Vector3 startPos = new Vector3(strikePos.x - riseHeight, strikePos.y - riseHeight, 0f);
        Vector3 endPos   = new Vector3(strikePos.x + riseHeight * 0.25f, strikePos.y + riseHeight * 0.25f, 0f);
        fist.transform.position = startPos;

        var hitSet = new HashSet<EnemyEntity>();
        float elapsed = 0f;
        float dmgBase = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f)
                        * (1f + RunUpgrades.DamageBonus);
        // Knockback equals the weapon level (1 at L1, 2 at L2 … 5 at L5)
        float knockbackForce = (float)w.level;

        while (elapsed < riseDuration) {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);
            // Ease-out interpolation so it slams to a halt at the end
            float eased = 1f - (1f - t) * (1f - t);
            fist.transform.position = Vector3.Lerp(startPos, endPos, eased);

            // Check overlaps every frame using a physics circle at fist centre
            float radius = fist.transform.localScale.x * 0.4f;
            var hits = Physics2D.OverlapCircleAll(fist.transform.position, radius);
            foreach (var col in hits) {
                if (!col.CompareTag("Enemy")) continue;
                var e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead || hitSet.Contains(e)) continue;
                hitSet.Add(e);
                e.TakeDamage(dmgBase);
                // Knock away from the fist centre
                Vector2 knockDir = ((Vector2)(e.transform.position - fist.transform.position)).normalized;
                if (knockDir == Vector2.zero) knockDir = Vector2.up;
                e.ApplyKnockback(knockDir, knockbackForce);
            }
            yield return null;
        }

        // Brief flash then destroy
        yield return new WaitForSeconds(0.15f);
        if (fist != null) Destroy(fist);
    }

    // Freeze + disable are checked in Update before ticking cooldowns.
    // (Berserker mult already calculated; just bail or skip weapon.)

    public void RapidFire(float d) { StartCoroutine(RapidFireRoutine(d)); }
    IEnumerator RapidFireRoutine(float duration) {
        // Drain all cooldown timers so every weapon fires immediately, then
        // keep them near zero for the duration (effectively fires ~5× faster).
        var keys = new List<string>(cooldowns.Keys);
        foreach (var k in keys) cooldowns[k] = 0f;
        float elapsed = 0f;
        while (elapsed < duration) {
            foreach (var k in new List<string>(cooldowns.Keys))
                if (cooldowns[k] > 0.1f) cooldowns[k] = 0f;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
