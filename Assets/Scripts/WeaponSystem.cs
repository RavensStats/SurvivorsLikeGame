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
        if (w.fireMode == FireMode.RisingFist)     { StartCoroutine(FireRisingFist(w));     return; }
        if (w.fireMode == FireMode.ChainLightning)  { StartCoroutine(FireChainLightning(w)); return; }
        if (w.fireMode == FireMode.VoidOrb)         { StartCoroutine(FireVoidOrb(w));         return; }
        if (w.fireMode == FireMode.HolySword)        { StartCoroutine(FireHolySword(w));        return; }
        if (w.fireMode == FireMode.AnimatedStrike)   { StartCoroutine(FireAnimatedStrike(w));   return; }
        if (w.fireMode == FireMode.OracleBeam)        { StartCoroutine(FireOracleBeam(w));        return; }
        if (w.fireMode == FireMode.SpectralBeam)      { StartCoroutine(FireSpectralBeam(w));      return; }
        if (w.fireMode == FireMode.TidalWave)          { StartCoroutine(FireTidalWave(w));          return; }

        if (w.projectilePrefab == null && string.IsNullOrEmpty(w.spriteFolder)) { Debug.LogWarning($"[WeaponSystem] '{w.itemName}' has no projectilePrefab or spriteFolder assigned."); return; }
        switch (w.fireMode) {
            case FireMode.NearestN:      FireNearestN(w);      break;
            case FireMode.RandomInRange: FireRandomInRange(w); break;
            default:                     FireDefault(w);       break;
        }
    }

    // Returns all sprite frames for a weapon spriteFolder, sorted by name for correct frame order.
    // Supports two layouts:
    //   (a) A single sprite-sheet asset (spriteMode:2) with multiple slices — loaded by file path.
    //   (b) Individual per-frame PNGs (Name_0.png, Name_1.png…) in the weapon folder — loaded by folder path.
    // Unity does NOT decode animated GIFs into multiple frames; use individual PNGs for animation.
    static Sprite[] LoadWeaponSprites(string spriteFolder) {
        if (string.IsNullOrEmpty(spriteFolder)) return null;

        // (a) Try loading sub-sprites from the specific asset (handles sprite-sheet PNGs).
        string assetPath = spriteFolder.Contains("/")
            ? $"Sprites/{spriteFolder}"
            : $"Sprites/Weapons/{spriteFolder}/{spriteFolder}";
        Sprite[] frames = Resources.LoadAll<Sprite>(assetPath);

        // (b) If the asset only yielded 0–1 sprites, try loading every sprite in the folder.
        //     This picks up individual frame PNGs (Name_0.png, Name_1.png, …).
        if (frames == null || frames.Length <= 1) {
            string folderPath = spriteFolder.Contains("/")
                ? $"Sprites/{System.IO.Path.GetDirectoryName(spriteFolder).Replace('\\', '/')}"
                : $"Sprites/Weapons/{spriteFolder}";
            Sprite[] folderFrames = Resources.LoadAll<Sprite>(folderPath);
            if (folderFrames != null && folderFrames.Length > (frames?.Length ?? 0))
                frames = folderFrames;
        }

        // Sort by name so frames always play in the correct order regardless of load order.
        if (frames != null && frames.Length > 1)
            System.Array.Sort(frames, (a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        if (frames != null && frames.Length > 0) return frames;

        // Final fallback: single directional sprite (e.g. Arrow East).
        Sprite east = Resources.Load<Sprite>($"Sprites/Weapons/{spriteFolder}/East");
        return east != null ? new[] { east } : null;
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
                Sprite[] swingFrames = LoadWeaponSprites(w.spriteFolder);
                if (swingFrames != null && swingFrames.Length > 0)
                    StartCoroutine(SwordSlide(swingFrames[0], pos, swingDir, range));
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
        Sprite[] scytheFrames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (scytheFrames != null && scytheFrames.Length > 0) ? scytheFrames[0] : null;

        var scythe = new GameObject("ScytheSwipe_VFX");
        var sr     = scythe.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 7;
        if (spr != null) {
            sr.sprite = spr;
            if (scytheFrames.Length > 1) { var anim = scythe.AddComponent<WeaponSpriteAnimator>(); anim.Init(scytheFrames, duration: swipeDuration); }
        }
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
            Sprite[] orbFrames = LoadWeaponSprites(w.spriteFolder);
            if (orbFrames != null && orbFrames.Length > 0) {
                sr.sprite = orbFrames[0];
                orb.transform.localScale = Vector3.one * 4f;   // 400 % scale for sprite orbs
                if (orbFrames.Length > 1) { var anim = orb.AddComponent<WeaponSpriteAnimator>(); anim.Init(orbFrames); }
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

    // Arc lightning beam from origin to target, stretched and rotated to fit.
    // The sprite is oriented vertically, so we add 90° to align its long axis with the beam.
    // displayDuration: how long the beam will live — used to scale fps so all GIF frames are shown exactly once.
    GameObject CreateLightningBeam(Vector3 from, Vector3 to, Sprite[] frames, float displayDuration) {
        var go = new GameObject("ChainLightningBeam");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        sr.sprite = frames[0];
        go.transform.position = (from + to) * 0.5f;
        float dist  = Vector2.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
        float naturalH = frames[0].rect.height / frames[0].pixelsPerUnit;
        float scaleY   = naturalH > 0f ? dist / naturalH : 1f;
        go.transform.localScale = new Vector3(5f, scaleY, 1f);
        if (frames.Length > 1) { var anim = go.AddComponent<WeaponSpriteAnimator>(); anim.Init(frames, duration: displayDuration); }
        return go;
    }

    // Returns the nearest alive, unstruk enemy within range of origin, or null.
    EnemyEntity FindNearestUnstruck(Vector3 origin, HashSet<EnemyEntity> struck, float range) {
        var candidates = SurvivorMasterScript.Instance.Grid.GetNearby(origin);
        float rangeSq = range * range;
        EnemyEntity best = null;
        float bestSq = float.MaxValue;
        foreach (var e in candidates) {
            if (e == null || e.isDead || struck.Contains(e)) continue;
            float d = (e.transform.position - origin).sqrMagnitude;
            if (d <= rangeSq && d < bestSq) { bestSq = d; best = e; }
        }
        return best;
    }

    // Fires one bolt to a random enemy in range, then chains up to w.level additional times.
    // Each hop shows a stretched GIF beam for 0.2 s before moving to the next target.
    IEnumerator FireChainLightning(ItemData w) {
        Vector3 playerPos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(playerPos);
        targets.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        float rangeSq = w.range * w.range;
        targets = targets.Where(e => (e.transform.position - playerPos).sqrMagnitude <= rangeSq).ToList();
        if (targets.Count == 0) yield break;

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
        var struck = new HashSet<EnemyEntity>();
        EnemyEntity cur = targets[Random.Range(0, targets.Count)];
        Vector3 strikeFrom = playerPos;

        int totalStrikes = w.level + 1; // first strike + w.level chains
        const float hopDuration = 0.2f; // TEST: slowed down to verify all GIF frames display
        for (int i = 0; i < totalStrikes; i++) {
            if (cur == null || cur.isDead || struck.Contains(cur)) break;
            cur.TakeDamage(dmg);
            struck.Add(cur);
            GameObject beam = (frames != null && frames.Length > 0)
                ? CreateLightningBeam(strikeFrom, cur.transform.position, frames, hopDuration) : null;
            strikeFrom = cur.transform.position;
            yield return new WaitForSeconds(hopDuration);
            if (beam != null) Destroy(beam);
            if (i < totalStrikes - 1)
                cur = FindNearestUnstruck(strikeFrom, struck, w.range);
        }
    }

    // Fires a single spinning VoidOrb projectile toward the nearest enemy.
    // On hit: deals damage, then splits into (level + 2) smaller orbs.
    // Each split orb arcs outward in a parabolic bounce to a random point on a circle
    // of randomized radius around the hit location, then despawns.
    IEnumerator FireVoidOrb(ItemData w) {
        Vector3 origin = PlayerPos;

        // Find nearest on-screen enemy.
        var candidates = SurvivorMasterScript.Instance.Grid.GetNearby(origin);
        candidates.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        if (candidates.Count == 0) yield break;
        candidates.Sort((a, b) =>
            (a.transform.position - origin).sqrMagnitude
            .CompareTo((b.transform.position - origin).sqrMagnitude));
        EnemyEntity target = candidates[0];

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;

        // Build the main orb GameObject.
        var orb = new GameObject("VoidOrb_Main");
        orb.transform.position = origin;
        var sr = orb.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        if (spr != null) sr.sprite = spr;
        float orbScale = w.projectileScale > 0f ? w.projectileScale : 5f;
        orb.transform.localScale = Vector3.one * orbScale;

        const float speed     = 12f;
        const float spinSpeed = 360f;  // degrees per second
        const float hitRadius = 1.0f;
        bool didHit = false;

        while (orb != null) {
            if (target == null || target.isDead) { Destroy(orb); yield break; }

            Vector2 delta = (Vector2)(target.transform.position - orb.transform.position);
            float step = speed * Time.deltaTime;
            orb.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            if (delta.magnitude <= step + hitRadius) {
                didHit = true;
                break; // close enough — register the hit
            }
            orb.transform.position += (Vector3)(delta.normalized * step);
            yield return null;
        }

        if (!didHit || orb == null) { if (orb != null) Destroy(orb); yield break; }

        Vector3 hitPos = target.transform.position;
        Destroy(orb);

        // Deal main-orb damage.
        float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
        if (!target.isDead) {
            target.TakeDamage(dmg);
        }

        // Spawn (level + 2) split orbs, each landing on a random point on a circle of
        // randomized radius around the hit position.
        int splitCount  = w.level + 2;
        float splitScale = orbScale * 1.0f;
        float splitDmg   = dmg * 0.5f;
        for (int i = 0; i < splitCount; i++) {
            float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(0.5f, 7.5f);
            Vector3 landAt = hitPos + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            StartCoroutine(VoidOrbSplitArc(hitPos, landAt, spr, splitScale, splitDmg));
        }
    }

    // Moves a split VoidOrb in a parabolic arc from 'from' to 'to', spinning as it travels.
    // Deals splitDmg to any enemy at the landing point, then despawns.
    IEnumerator VoidOrbSplitArc(Vector3 from, Vector3 to, Sprite spr, float scale, float splitDmg) {
        var go = new GameObject("VoidOrb_Split");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        if (spr != null) sr.sprite = spr;
        go.transform.localScale = Vector3.one * scale;

        float duration  = Random.Range(0.35f, 0.6f);
        float arcHeight = Random.Range(0.8f, 2.0f);
        // Randomize spin direction so orbs feel chaotic.
        float spinSpeed = Random.Range(200f, 500f) * (Random.value < 0.5f ? 1f : -1f);
        float elapsed   = 0f;

        while (elapsed < duration) {
            if (go == null) yield break;
            float t   = elapsed / duration;
            // Lerp horizontally, add a sine-curve vertical arc (peaks at t=0.5, zero at t=0 and t=1).
            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y += arcHeight * Mathf.Sin(t * Mathf.PI);
            go.transform.position = pos;
            go.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);

        // Deal damage to the first enemy found at the landing point.
        if (splitDmg > 0f) {
            foreach (var col in Physics2D.OverlapCircleAll(to, 0.6f)) {
                if (!col.CompareTag("Enemy")) continue;
                var e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead) continue;
                e.TakeDamage(splitDmg);
                break;
            }
        }
    }

    // Tracks which enemies are currently being targeted by an in-flight HolySword strike,
    // so concurrent strikes never double-target the same enemy.
    private readonly HashSet<EnemyEntity> _holySwordTargeted = new HashSet<EnemyEntity>();

    // Spawns w.level simultaneous HolySword strikes, each targeting the nearest un-targeted enemy.
    // Each sword appears above the target, drops rapidly downward, damages all enemies it passes
    // through, then despawns.
    IEnumerator FireHolySword(ItemData w) {
        Vector3 playerPos = PlayerPos;
        var candidates = SurvivorMasterScript.Instance.Grid.GetNearby(playerPos);
        candidates.RemoveAll(e => e == null || e.isDead
            || !SurvivorMasterScript.IsOnScreen(e.transform.position)
            || _holySwordTargeted.Contains(e));
        candidates.Sort((a, b) =>
            (a.transform.position - playerPos).sqrMagnitude
            .CompareTo((b.transform.position - playerPos).sqrMagnitude));

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;
        float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        int strikes = Mathf.Min(w.level, candidates.Count);
        for (int i = 0; i < strikes; i++) {
            EnemyEntity target = candidates[i];
            _holySwordTargeted.Add(target);
            StartCoroutine(HolySwordDrop(target, spr, dmg, w.knockback));
        }
        yield break;
    }

    // Drops a single HolySword sprite from above the target downward, dealing damage
    // to every enemy whose collider it overlaps during the fall, then despawns.
    IEnumerator HolySwordDrop(EnemyEntity target, Sprite spr, float dmg, float knockback) {
        const float spawnHeightAboveTarget = 5f;   // world-units above target
        const float dropDistance           = 8f;   // total downward travel
        const float dropSpeed              = 22f;  // world-units per second
        const float hitRadius             = 0.7f;  // overlap check radius

        Vector3 startPos = target.transform.position + Vector3.up * spawnHeightAboveTarget;

        var go = new GameObject("HolySword_VFX");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 9;
        if (spr != null) sr.sprite = spr;
        go.transform.position = startPos;
        // Scale to a reasonable world size (~2 units tall).
        if (spr != null) {
            float naturalH = spr.rect.height / spr.pixelsPerUnit;
            float scale = naturalH > 0f ? 10f / naturalH : 1f;
            go.transform.localScale = Vector3.one * scale;
        }

        // Brief pause — sword tracks the enemy's live position during the hover.
        float hoverElapsed = 0f;
        const float hoverDuration = 0.35f;
        while (hoverElapsed < hoverDuration) {
            if (go == null) { _holySwordTargeted.Remove(target); yield break; }
            if (target != null && !target.isDead)
                go.transform.position = target.transform.position + Vector3.up * spawnHeightAboveTarget;
            hoverElapsed += Time.deltaTime;
            yield return null;
        }

        float traveled = 0f;
        var alreadyHit = new HashSet<EnemyEntity>();

        while (traveled < dropDistance) {
            if (go == null) break;
            float step = dropSpeed * Time.deltaTime;
            go.transform.position += Vector3.down * step;
            traveled += step;

            // Damage every enemy overlapping the sword's current position.
            foreach (var col in Physics2D.OverlapCircleAll(go.transform.position, hitRadius)) {
                if (!col.CompareTag("Enemy")) continue;
                var e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead || alreadyHit.Contains(e)) continue;
                alreadyHit.Add(e);
                e.TakeDamage(dmg);
                if (knockback > 0f) e.ApplyKnockback(Vector2.down, knockback);
            }

            yield return null;
        }

        _holySwordTargeted.Remove(target);
        if (go != null) Destroy(go);
    }

    // Generic animated-strike attack: finds up to w.level targets within w.range, spawns a sprite
    // VFX at each one that tracks the enemy and plays through all frames, then deals damage and despawns.
    // Used by: Spectral Beam, Oracle Beam, Psychic Blast, Creeping Vines, Tidal Wave,
    //           Sentry Gun, Blood Pool, Spirit Aura.
    IEnumerator FireAnimatedStrike(ItemData w) {
        const float displayDuration = 0.5f;

        Vector3 playerPos = PlayerPos;
        var candidates = SurvivorMasterScript.Instance.Grid.GetNearby(playerPos);
        candidates.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        if (w.range > 0f) {
            float rangeSq = w.range * w.range;
            candidates = candidates.Where(e => (e.transform.position - playerPos).sqrMagnitude <= rangeSq).ToList();
        }
        if (candidates.Count == 0) yield break;

        candidates.Sort((a, b) =>
            (a.transform.position - playerPos).sqrMagnitude
            .CompareTo((b.transform.position - playerPos).sqrMagnitude));

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        float dmg   = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
        float scale = w.projectileScale > 0f ? w.projectileScale : 3f;
        int count   = Mathf.Min(w.level, candidates.Count);

        for (int i = 0; i < count; i++)
            StartCoroutine(AnimatedStrikeVFX(candidates[i], frames, scale, displayDuration, dmg));

        yield break;
    }

    // Spawns an animated VFX at the target enemy, tracks its position, plays all frames over
    // displayDuration, deals damage immediately, then despawns.
    IEnumerator AnimatedStrikeVFX(EnemyEntity target, Sprite[] frames, float scale, float duration, float dmg) {
        if (target == null || target.isDead) yield break;

        // Deal damage up-front (like ChainLightning).
        target.TakeDamage(dmg);

        if (frames == null || frames.Length == 0) yield break;

        var go = new GameObject("AnimatedStrike_VFX");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        sr.sprite = frames[0];
        go.transform.localScale = Vector3.one * scale;
        go.transform.position = target != null ? target.transform.position : go.transform.position;

        if (frames.Length > 1) {
            var anim = go.AddComponent<WeaponSpriteAnimator>();
            anim.Init(frames, duration: duration);
        }

        float elapsed = 0f;
        while (elapsed < duration) {
            if (go == null) yield break;
            // Track the enemy's live position so the VFX stays centred on them.
            if (target != null && !target.isDead)
                go.transform.position = target.transform.position;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // Tracks which enemies are currently being targeted by an in-flight OracleBeam,
    // so concurrent beams never double-target the same enemy.
    private readonly HashSet<EnemyEntity> _oracleBeamTargeted = new HashSet<EnemyEntity>();

    // Persistent angle for SpectralBeam; advances 45° CCW each attack.
    private float _spectralBeamAngle = 0f;

    // Fires w.level simultaneous OracleBeam strikes at the nearest un-targeted enemies.
    IEnumerator FireOracleBeam(ItemData w) {
        Vector3 playerPos = PlayerPos;
        var candidates = SurvivorMasterScript.Instance.Grid.GetNearby(playerPos);
        candidates.RemoveAll(e => e == null || e.isDead
            || !SurvivorMasterScript.IsOnScreen(e.transform.position)
            || _oracleBeamTargeted.Contains(e));
        if (w.range > 0f) {
            float rangeSq = w.range * w.range;
            candidates = candidates.Where(e => (e.transform.position - playerPos).sqrMagnitude <= rangeSq).ToList();
        }
        candidates.Sort((a, b) =>
            (a.transform.position - playerPos).sqrMagnitude
            .CompareTo((b.transform.position - playerPos).sqrMagnitude));

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        int strikes = Mathf.Min(w.level, candidates.Count);
        for (int i = 0; i < strikes; i++) {
            EnemyEntity target = candidates[i];
            _oracleBeamTargeted.Add(target);
            StartCoroutine(OracleBeamVFX(target, frames, dmg));
        }
        yield break;
    }

    // Hovers the OracleBeam sprite above the target's head, tracking their live position,
    // plays through all frames once, deals damage, then despawns.
    IEnumerator OracleBeamVFX(EnemyEntity target, Sprite[] frames, float dmg) {
        const float heightAboveHead = 7f;     // world-units above enemy centre (anchors the top of the last frame)
        const float displayDuration = 0.6f;   // total time the beam is visible
        const float spriteScale     = 30f;    // uniform scale applied to sprite

        if (frames == null || frames.Length == 0) {
            if (target != null && !target.isDead) target.TakeDamage(dmg);
            _oracleBeamTargeted.Remove(target);
            yield break;
        }

        var go = new GameObject("OracleBeam_VFX");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 9;
        sr.sprite = frames[0];
        go.transform.localScale = Vector3.one * spriteScale;

        // Pre-compute the scaled height of every frame so we can do top-anchor math each frame.
        // The "fixed top" is the top edge of the last (tallest/full) frame, held at heightAboveHead
        // above the enemy.  Each frame is repositioned so its top stays at that same world-Y.
        float[] frameHeights = new float[frames.Length];
        for (int fi = 0; fi < frames.Length; fi++)
            frameHeights[fi] = (frames[fi].rect.height / frames[fi].pixelsPerUnit) * spriteScale;

        float lastFrameH = frameHeights[frames.Length - 1];

        // Manual frame timer (mirrors WeaponSpriteAnimator logic but with top-anchor repositioning).
        float frameInterval = frames.Length > 1 ? displayDuration / frames.Length : displayDuration;
        int   currentFrame  = 0;
        float frameTimer    = 0f;
        float elapsed       = 0f;

        while (elapsed < displayDuration) {
            if (go == null) { _oracleBeamTargeted.Remove(target); yield break; }

            // Determine the world-Y of the fixed top edge: enemy Y + heightAboveHead puts the
            // top of the last frame there.  top = anchorTopY, so centre = top - halfH.
            Vector3 enemyPos = (target != null && !target.isDead)
                ? target.transform.position
                : go.transform.position;
            float anchorTopY = enemyPos.y + heightAboveHead;
            float halfH      = frameHeights[currentFrame] * 0.5f;
            go.transform.position = new Vector3(enemyPos.x, anchorTopY - halfH, 0f);

            // Advance frame.
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameInterval && currentFrame < frames.Length - 1) {
                frameTimer -= frameInterval;
                currentFrame++;
                sr.sprite = frames[currentFrame];
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Deal damage once the full animation has played.
        if (target != null && !target.isDead)
            target.TakeDamage(dmg);

        _oracleBeamTargeted.Remove(target);
        if (go != null) Destroy(go);
    }

    // Fires a spectral beam from the player outward at _spectralBeamAngle,
    // rotating 45° CCW each attack. Cooldown decreases with level.
    IEnumerator FireSpectralBeam(ItemData w)
    {
        // Scale cooldown: level 1 = baseCooldown, level 5 = 20% of baseCooldown.
        float baseCooldown = 1.4f;
        w.cooldown = Mathf.Lerp(baseCooldown, baseCooldown * 0.2f, (w.level - 1) / 4f);

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        if (frames == null || frames.Length == 0) yield break;

        float displayDuration = 0.75f;
        float beamAngle = _spectralBeamAngle; // snapshot angle for this attack
        _spectralBeamAngle = (_spectralBeamAngle + 45f) % 360f;

        // Direction the beam points (CCW from right).
        float angleRad = beamAngle * Mathf.Deg2Rad;
        Vector2 beamDir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

        float scale = 35f;
        const float gapFromPlayer = 2.5f;

        // Pre-compute scaled dimensions per frame (frames may vary in height).
        float[] frameHeights = new float[frames.Length];
        float[] frameWidths  = new float[frames.Length];
        for (int fi = 0; fi < frames.Length; fi++)
        {
            float ppu = frames[fi].pixelsPerUnit;
            frameHeights[fi] = (frames[fi].rect.height / ppu) * scale;
            frameWidths[fi]  = (frames[fi].rect.width  / ppu) * scale;
        }

        // The bottom (player-side) edge anchors gapFromPlayer units ahead of the player,
        // recalculated every frame so the beam tracks player movement.
        // Rotation: local +Y along beamDir, so local -Y (bottom) points back toward player.
        float rotZ = Mathf.Atan2(beamDir.y, beamDir.x) * Mathf.Rad2Deg - 90f;

        // Spawn the beam GameObject.
        GameObject go = new GameObject("SpectralBeam");
        go.transform.rotation   = Quaternion.Euler(0f, 0f, rotZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite          = frames[0];
        sr.sortingLayerName = "Default";
        sr.sortingOrder    = 5;

        // Manual frame loop — repositions sprite center so bottom edge tracks with player.
        float dmg           = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
        var   alreadyHit    = new HashSet<EnemyEntity>();
        float frameInterval = displayDuration / frames.Length;
        int   currentFrame  = 0;
        float frameTimer    = 0f;
        float elapsed       = 0f;

        while (elapsed < displayDuration)
        {
            if (go == null) yield break;

            // Recompute anchor from live player position each frame.
            Vector3 anchorBottom = PlayerPos + (Vector3)(beamDir * gapFromPlayer);

            // Pin bottom edge: center = anchorBottom + beamDir * halfHeight.
            float halfH = frameHeights[currentFrame] * 0.5f;
            go.transform.position = anchorBottom + (Vector3)(beamDir * halfH);

            // Hit detection using current frame's exact box.
            foreach (Collider2D col in Physics2D.OverlapBoxAll(
                go.transform.position,
                new Vector2(frameWidths[currentFrame], frameHeights[currentFrame]),
                rotZ))
            {
                if (!col.CompareTag("Enemy")) continue;
                EnemyEntity e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead || alreadyHit.Contains(e)) continue;
                alreadyHit.Add(e);
                e.TakeDamage(dmg);
            }

            // Advance frame.
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameInterval && currentFrame < frames.Length - 1)
            {
                frameTimer -= frameInterval;
                currentFrame++;
                sr.sprite = frames[currentFrame];
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // Fires multiple wave projectiles outward from the player at random angles.
    // Wave count: 3 at level 1, +1 per level up to level 4, then +5 at level 5 (total 11).
    IEnumerator FireTidalWave(ItemData w)
    {
        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        if (frames == null || frames.Length == 0) yield break;

        int waveCount = w.level switch
        {
            1 => 3,
            2 => 4,
            3 => 5,
            4 => 6,
            _ => 11  // level 5+
        };

        float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        for (int i = 0; i < waveCount; i++)
        {
            float angle = Random.Range(0f, 360f);
            StartCoroutine(TidalWaveProjectile(w, frames, angle, dmg));
        }

        yield break;
    }

    IEnumerator TidalWaveProjectile(ItemData w, Sprite[] frames, float angleDeg, float dmg)
    {
        const float startOffset  = 2f;   // world units from player at spawn
        const float spriteScale  = 6f;
        const float moveSpeed    = 10f;
        const float animDuration = 0.4f; // time to play all frames once

        float angleRad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir    = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        float maxRange = w.range;

        // Measure native sprite size.
        float ppu         = frames[0].pixelsPerUnit;
        float spriteW     = (frames[0].rect.width  / ppu) * spriteScale;
        float spriteH     = (frames[0].rect.height / ppu) * spriteScale;

        // Rotate: sprite +X points along travel direction.
        float rotZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        GameObject go = new GameObject("TidalWave");
        go.transform.rotation   = Quaternion.Euler(0f, 0f, rotZ);
        go.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = frames[0];
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 5;
        // Flip vertically for waves traveling between 90° and 270° so the sprite stays upright.
        sr.flipY = angleDeg >= 90f && angleDeg <= 270f;

        float frameInterval = frames.Length > 1 ? animDuration / frames.Length : animDuration;
        int   currentFrame  = 0;
        float frameTimer    = 0f;

        float traveled   = 0f;
        var   alreadyHit = new HashSet<EnemyEntity>();

        // Start just outside the player.
        Vector3 spawnPos = PlayerPos + (Vector3)(dir * startOffset);
        go.transform.position = spawnPos;
        traveled = 0f;

        while (traveled < maxRange)
        {
            if (go == null) yield break;

            float step = moveSpeed * Time.deltaTime;
            go.transform.position += (Vector3)(dir * step);
            traveled += step;

            // Damage enemies overlapping the sprite's current footprint.
            foreach (Collider2D col in Physics2D.OverlapBoxAll(
                go.transform.position,
                new Vector2(spriteW, spriteH),
                rotZ))
            {
                if (!col.CompareTag("Enemy")) continue;
                EnemyEntity e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead || alreadyHit.Contains(e)) continue;
                alreadyHit.Add(e);
                e.TakeDamage(dmg);
            }

            // Advance animation (loops if wave travels further than one cycle).
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameInterval)
            {
                frameTimer -= frameInterval;
                currentFrame = (currentFrame + 1) % frames.Length;
                sr.sprite = frames[currentFrame];
            }

            yield return null;
        }

        if (go != null) Destroy(go);
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
