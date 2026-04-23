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
    private readonly List<GameObject> _activeSentries = new List<GameObject>();
    private GameObject _sniperReticle;
    private GameObject _windstorm;

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
        // Destroy any placed sentry guns.
        foreach (var s in _activeSentries) if (s != null) Destroy(s);
        _activeSentries.Clear();
        // Destroy sniper reticle.
        if (_sniperReticle != null) { Destroy(_sniperReticle); _sniperReticle = null; }
        // Destroy windstorm.
        if (_windstorm != null) { Destroy(_windstorm); _windstorm = null; }
        // Destroy any active insect swarms.
        foreach (var s in InsectSwarmLogic.Active)
            if (s != null) Destroy(s.gameObject);
        InsectSwarmLogic.Active.Clear();
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
        if (w.fireMode == FireMode.SentryGun)        { FireSentryGun(w);                        return; }
        if (w.fireMode == FireMode.CaltropThrow)     { FireCaltropThrow(w);                     return; }
        if (w.fireMode == FireMode.KatanaSlash)      { StartCoroutine(FireKatanaSlash(w));       return; }
        if (w.fireMode == FireMode.DualRevolvers)    { StartCoroutine(FireDualRevolvers(w));     return; }
        if (w.fireMode == FireMode.SniperReticle)    { MaintainSniperReticle(w);                 return; }
        if (w.fireMode == FireMode.BalladWave)       { StartCoroutine(FireBalladWave(w));        return; }
        if (w.fireMode == FireMode.WoodcutterAxe)    { FireWoodcutterAxe(w);                     return; }
        if (w.fireMode == FireMode.GravityWell)      { FireGravityWell(w);                       return; }
        if (w.fireMode == FireMode.Windstorm)        { MaintainWindstorm(w);                     return; }
        if (w.fireMode == FireMode.MeteorStrike)     { FireMeteorStrike(w);                      return; }
        if (w.fireMode == FireMode.InsectSwarm)      { FireInsectSwarm(w);                       return; }
        if (w.fireMode == FireMode.SawBlade)         { FireSawBlade(w);                          return; }
        if (w.fireMode == FireMode.PoisonDagger)     { StartCoroutine(FirePoisonDagger(w));      return; }
        if (w.fireMode == FireMode.TridentStrike)    { StartCoroutine(FireTridentStrike(w));     return; }
        if (w.fireMode == FireMode.OracleBeam)        { StartCoroutine(FireOracleBeam(w));        return; }
        if (w.fireMode == FireMode.SpectralBeam)      { StartCoroutine(FireSpectralBeam(w));      return; }
        if (w.fireMode == FireMode.TidalWave)          { StartCoroutine(FireTidalWave(w));          return; }
        if (w.fireMode == FireMode.MagicAura)          { StartCoroutine(FireMagicAura(w));          return; }
        if (w.fireMode == FireMode.BloodPool)          { StartCoroutine(FireBloodPool(w));          return; }
        if (w.fireMode == FireMode.RangerArrow)        { FireRangerArrow(w);                        return; }
        if (w.fireMode == FireMode.PoisonPool)         { FirePoisonPool(w);                         return; }

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

    // Fires one flask at the single nearest enemy within range; AlchemistPool spawns on hit via ProjectileLogic.
    void FirePoisonPool(ItemData w) {
        Vector3 pos = PlayerPos;
        var candidates = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        candidates.RemoveAll(e => e == null || e.isDead);
        float rangeSq = w.range * w.range;
        EnemyEntity nearest = null;
        float bestSq = float.MaxValue;
        foreach (var e in candidates) {
            float d = (e.transform.position - pos).sqrMagnitude;
            if (d <= rangeSq && d < bestSq) { bestSq = d; nearest = e; }
        }
        if (nearest == null) return;
        Vector2 dir = (nearest.transform.position - pos).normalized;
        SpawnProjectile(w, pos).GetComponent<ProjectileLogic>().Setup(w, dir, nearest);
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

    // Artificer custom attack: fires 1–3 spinning saw blades at the nearest enemy.
    // Each blade bounces at a random ±45° angle on every enemy hit and flies until
    // it leaves the screen. Each enemy can only be struck once per blade.
    //
    // Level scaling:
    //   L2 – damage ×1.25 | L3 – +1 blade (2 total) | L4 – damage ×1.25 | L5 – +1 blade (3 total)
    //   Blade count: L1-L2 = 1, L3-L4 = 2, L5 = 3.
    //   Multiple blades fan out at ±15° (2 blades) or ±30°/0° (3 blades) from base direction.
    void FireSawBlade(ItemData w) {
        var target = FindNearestUnstruck(PlayerPos,
            new System.Collections.Generic.HashSet<EnemyEntity>(), w.range);
        if (target == null) return;

        float dmg = w.baseDamage;
        if (w.level >= 2) dmg *= 1.25f;
        if (w.level >= 4) dmg *= 1.25f;
        dmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;

        int   count    = 1 + (w.level >= 3 ? 1 : 0) + (w.level >= 5 ? 1 : 0);
        float baseAngle = Mathf.Atan2(
            target.transform.position.y - PlayerPos.y,
            target.transform.position.x - PlayerPos.x) * Mathf.Rad2Deg;

        // Fan offsets: 1 blade = {0°}, 2 blades = {-15°,+15°}, 3 blades = {-30°,0°,+30°}
        // offset = -(count-1)*15 + i*30  gives a 30° step centred on the target direction.
        for (int i = 0; i < count; i++) {
            float offset    = -(count - 1) * 15f + i * 30f;
            float rad       = (baseAngle + offset) * Mathf.Deg2Rad;
            var   dir       = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            SawBladeLogic.Spawn(PlayerPos, dir, dmg, spr);
        }
    }

    // Hivemaster custom attack: spawns (n*2 - 1) insect swarm units where n = weapon level.
    // Each unit independently hunts the nearest enemy and stings it every 2 s.
    // Units have 1 HP and die on the first hit from any enemy attack.
    //
    // Level scaling (damage, cumulative ×1.10 per level above 1):
    //   L1 – 1 insect | L2 – 3 (×1.10) | L3 – 5 (×1.21) | L4 – 7 (×1.331) | L5 – 9 (×1.4641)
    void FireInsectSwarm(ItemData w) {
        float dmg = w.baseDamage;
        if (w.level >= 2) dmg *= 1.10f;
        if (w.level >= 3) dmg *= 1.10f;
        if (w.level >= 4) dmg *= 1.10f;
        if (w.level >= 5) dmg *= 1.10f;
        dmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        int count = w.level * 2 - 1;
        for (int i = 0; i < count; i++) {
            Vector2 offset = Random.insideUnitCircle * 1.5f;
            InsectSwarmLogic.Spawn(PlayerPos + (Vector3)offset, dmg, w.spriteFolder);
        }
    }

    // Geomancer custom attack: calls n meteors (n = weapon level) from just above the
    // top of the screen, each targeting a random on-screen enemy. Meteors fly at the
    // enemy's position at the moment of firing; they stun on hit.
    //
    // Level scaling:
    //   L2 – damage ×1.10 | L3 – ×1.15 (cumul.) | L4 – ×1.20 (cumul.) | L5 – stun 1.5 s
    //   Meteor count = weapon level (1 at L1 … 5 at L5).
    void FireMeteorStrike(ItemData w) {
        var sms = SurvivorMasterScript.Instance;
        var candidates = sms.Grid.GetNearby(PlayerPos);
        candidates.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        if (candidates.Count == 0) return;

        // Fisher-Yates shuffle so selection is uniformly random.
        for (int i = candidates.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        float dmg = w.baseDamage;
        if (w.level >= 2) dmg *= 1.10f;
        if (w.level >= 3) dmg *= 1.15f;
        if (w.level >= 4) dmg *= 1.20f;
        dmg *= (sms?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
        float stunDuration = w.level >= 5 ? 1.5f : 1.0f;

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;

        Camera cam = Camera.main;
        float spawnY  = cam.transform.position.y + cam.orthographicSize + 2f;
        float leftX   = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        float rightX  = cam.transform.position.x + cam.orthographicSize * cam.aspect;

        int count = w.level; // 1 at L1, 5 at L5
        for (int i = 0; i < count; i++) {
            var target   = candidates[i % candidates.Count];
            float spawnX = Random.Range(leftX, rightX);
            var spawnPos = new Vector3(spawnX, spawnY, 0f);
            MeteorStrikeLogic.Spawn(spawnPos, target.transform.position, dmg, stunDuration, spr);
        }
    }

    // Aeromancer custom attack: creates one persistent WindstormLogic that orbits the
    // player counterclockwise and self-manages damage. Called on the 1 s cooldown tick
    // but is a no-op once the object exists.
    void MaintainWindstorm(ItemData w) {
        if (_windstorm != null) return;
        var go = new GameObject("WindstormRoot");
        var logic = go.AddComponent<WindstormLogic>();
        logic.weaponData = w;
        _windstorm = go;
    }

    // Gravity Manipulator custom attack: spawns an expanding/contracting ring at the
    // player's position. The ring frontier damages and forces enemies on both passes.
    // L4+ cooldown is reduced by 20% (set here so the timer picks it up immediately).
    //
    // Level scaling:
    //   L2 – damage ×1.25 | L3 – damage ×1.5 (cumulative) | L4 – cooldown ×0.8 | L5 – push+pull
    void FireGravityWell(ItemData w) {
        w.cooldown = 7.0f * (w.level >= 4 ? 0.8f : 1.0f);

        float dmg = w.baseDamage;
        if (w.level >= 2) dmg *= 1.25f;
        if (w.level >= 3) dmg *= 1.5f;
        dmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        GravityWellLogic.Spawn(PlayerPos, dmg, w.level, w.range);
    }

    // Viking custom attack: hurls a spinning axe at the nearest enemy.
    // On hit the axe deals damage, slows the target by 50%, then snaps to 330° and
    // rests on the ground for 0.5 s. Spin rate is calibrated so the axe always
    // completes exactly 3 full rotations and arrives at 330° at maximum range.
    //
    // Level scaling:
    //   L2 – slow duration +1 s (4 s total)
    //   L3 – damage ×1.5
    //   L4 – slow duration +1 s (5 s total)
    //   L5 – damage ×1.5 again (×2.25 total)
    void FireWoodcutterAxe(ItemData w) {
        var target = FindNearestUnstruck(PlayerPos, new System.Collections.Generic.HashSet<EnemyEntity>(), w.range);
        if (target == null) return;

        float dmg = w.baseDamage;
        if (w.level >= 3) dmg *= 1.5f;
        if (w.level >= 5) dmg *= 1.5f;
        dmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        float slowDuration = 3f + (w.level >= 2 ? 1f : 0f) + (w.level >= 4 ? 1f : 0f);

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;

        WoodcutterAxeLogic.Spawn(PlayerPos, target.transform.position, dmg, slowDuration, w.range, spr);
    }

    // Bard custom attack: fires 5 MusicNotes in rapid succession along the same random
    // direction, each following an identical sin wave path staggered 0.1 s apart.
    // Charm duration grows with level: L1 – 5 s | L2 – 6 s | L3 – 7 s | L4 – 8 s | L5 – 10 s
    IEnumerator FireBalladWave(ItemData w) {
        float charmDuration = 4f + w.level + (w.level >= 5 ? 1f : 0f);
        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        for (int i = 0; i < 5; i++) {
            BalladNoteLogic.Spawn(PlayerPos, dir, charmDuration, spr);
            yield return new WaitForSeconds(0.1f);
        }
    }

    // Sniper custom attack: creates one persistent SniperReticleLogic that self-manages
    // all targeting, animation, and cooldown. Called on the weapon's normal cooldown tick
    // (every 1 s) but is effectively a no-op when the reticle already exists.
    void MaintainSniperReticle(ItemData w) {
        if (_sniperReticle != null) return;
        var go = new GameObject("SniperReticleRoot");
        var logic = go.AddComponent<SniperReticleLogic>();
        logic.weaponData = w;
        _sniperReticle = go;
    }

    // Gunslinger custom attack: fires pairs of cannonballs in a 180° world-left arc
    // (90°–270°, where 0° = right, 180° = left). Each pair fires simultaneously;
    // subsequent pairs are staggered by 100 ms. Each cannonball damages and despawns
    // on the first enemy it hits.
    //
    // Level scaling (cumulative ×1.25 damage, +2 cannonballs per level above 1):
    //   L1 – 2 balls (1 pair)  | L2 – 4 | L3 – 6 | L4 – 8 | L5 – 10
    IEnumerator FireDualRevolvers(ItemData w) {
        const float cooldown   = 2.0f;
        const float pairDelay  = 0.1f;   // 100 ms between pairs
        const float arcMinDeg  = 90f;    // world-left 180° arc: 90° → 270°
        const float arcMaxDeg  = 270f;

        // Set cooldown before first yield so Update picks it up immediately.
        w.cooldown = cooldown;

        float dmg = w.baseDamage * Mathf.Pow(1.25f, w.level - 1)
                  * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f)
                  * (1f + RunUpgrades.DamageBonus);

        // L1 = 1 pair; each level adds 1 more pair (2 more cannonballs).
        int pairs = w.level;

        for (int pair = 0; pair < pairs; pair++) {
            if (pair > 0)
                yield return new WaitForSeconds(pairDelay);

            Vector3 origin = PlayerPos;
            for (int i = 0; i < 2; i++) {
                float angle = Random.Range(arcMinDeg, arcMaxDeg) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                CannonballLogic.Spawn(origin, dir, dmg, 0f);
            }
        }
    }

    // Samurai custom attack: sweeps the katana sprite through a random 90° arc around the player,
    // with the sprite's top (+Y) facing outward, over 0.35 s. Every enemy the blade passes through
    // is hit once per swing. Damage and cooldown compound by ×1.25 / ×0.75 per level above 1.
    IEnumerator FireKatanaSlash(ItemData w) {
        const float swingDuration = 0.35f;
        const float sweepDegrees  = 90f;

        // Cooldown and damage both compound ×0.75 / ×1.25 each level above 1.
        float baseCooldown = 1.0f;
        w.cooldown = baseCooldown * Mathf.Pow(0.75f, w.level - 1);

        float dmg = w.baseDamage * Mathf.Pow(1.25f, w.level - 1)
                  * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f)
                  * (1f + RunUpgrades.DamageBonus);

        float startAngle = Random.Range(0f, 360f);
        float range      = w.range > 0f ? w.range : 10f;

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;

        var go = new GameObject("KatanaSlash_VFX");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 7;
        if (spr != null) sr.sprite = spr;

        // Scale sprite so its natural height fills the full range length.
        float spriteScale = 10f;
        if (spr != null) {
            float naturalH = spr.rect.height / spr.pixelsPerUnit;
            if (naturalH > 0f) spriteScale = range / naturalH;
        }
        go.transform.localScale = Vector3.one * spriteScale;

        // Sprite centre orbits at half-range so the blade tip reaches the full range.
        float orbitRadius = range * 0.5f;
        float hitRadius   = range * 0.25f;

        var hitSet = new HashSet<EnemyEntity>();
        float elapsed = 0f;

        while (elapsed < swingDuration) {
            if (go == null) yield break;
            var player = SurvivorMasterScript.Instance?.player;
            if (player == null) break;

            float t            = elapsed / swingDuration;
            float currentAngle = startAngle + sweepDegrees * t;
            float rad          = currentAngle * Mathf.Deg2Rad;
            Vector2 outDir     = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            go.transform.position = (Vector2)player.position + outDir * orbitRadius;
            // Align sprite so local +Y (top) points outward along outDir.
            go.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);

            foreach (var col in Physics2D.OverlapCircleAll(go.transform.position, hitRadius)) {
                if (!col.CompareTag("Enemy")) continue;
                var e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead || hitSet.Contains(e)) continue;
                hitSet.Add(e);
                e.TakeDamage(dmg);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // Tactician custom attack: throws N caltrops per attack in parabolic arcs to random
    // screen positions. Caltrops persist on the ground and trigger-damage the first enemy
    // that steps on them, then despawn. Destroyed automatically when off-screen.
    //
    // Level scaling:
    //   L1 – 3 thrown, base damage, base cooldown (1.5 s)
    //   L2 – 4 thrown, ×1.25 damage
    //   L3 – 5 thrown
    //   L4 – 7 thrown, ×1.25 attack speed
    //   L5 – 10 thrown
    //   Max caltrops on screen = level × 4; if throwing would exceed cap, reduce count.
    void FireCaltropThrow(ItemData w) {
        const float baseCooldown = 1.5f;
        w.cooldown = w.level >= 4 ? baseCooldown * 0.75f : baseCooldown;

        int baseThrow = w.level switch {
            1 => 3,
            2 => 4,
            3 => 5,
            4 => 7,
            _ => 10   // level 5
        };

        int maxOnScreen = w.level * 4;
        int currentOnScreen = 0;
        foreach (var c in CaltropLogic.Active)
            if (c != null && SurvivorMasterScript.IsOnScreen(c.transform.position)) currentOnScreen++;

        int toThrow = Mathf.Max(0, Mathf.Min(baseThrow, maxOnScreen - currentOnScreen));
        if (toThrow == 0) return;

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;

        float dmg = w.baseDamage;
        if (w.level >= 2) dmg *= 1.25f;
        dmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        Vector3 playerPos = PlayerPos;

        for (int i = 0; i < toThrow; i++) {
            Vector3 landPos = Vector3.positiveInfinity;
            for (int attempt = 0; attempt < 15; attempt++) {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist  = Random.Range(6f, 14f);
                Vector3 candidate = playerPos + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
                if (SurvivorMasterScript.IsOnScreen(candidate)) { landPos = candidate; break; }
            }
            if (landPos == Vector3.positiveInfinity) continue;
            StartCoroutine(CaltropArc(playerPos, landPos, spr, dmg));
        }
    }

    // Animates a caltrop sprite in a parabolic arc from 'from' to 'to', then spawns
    // a persistent CaltropLogic at the landing position.
    IEnumerator CaltropArc(Vector3 from, Vector3 to, Sprite spr, float dmg) {
        var go = new GameObject("CaltropInFlight");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 8;
        if (spr != null) sr.sprite = spr;
        go.transform.localScale = Vector3.one * 3f;

        float duration  = Random.Range(0.4f, 0.7f);
        float arcHeight = Random.Range(1.5f, 3.0f);
        float spinSpeed = Random.Range(200f, 400f) * (Random.value < 0.5f ? 1f : -1f);
        float elapsed   = 0f;

        while (elapsed < duration) {
            if (go == null) yield break;
            float t   = elapsed / duration;
            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y += arcHeight * Mathf.Sin(t * Mathf.PI);
            go.transform.position = pos;
            go.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
        CaltropLogic.Spawn(to, dmg, spr);
    }

    // Engineer custom attack: places up to N sentry guns near the player (N depends on level).
    // Each sentry manages its own animated turret and fires cannonballs independently.
    // Level scaling: L2 +25% dmg | L3 2nd sentry | L4 +25% attack speed | L5 3rd sentry
    void FireSentryGun(ItemData w) {
        // Cull nulls and sentries that drifted too far from the player (> 35 units).
        Vector3 playerPos = PlayerPos;
        _activeSentries.RemoveAll(s => {
            if (s == null) return true;
            if ((s.transform.position - playerPos).sqrMagnitude > 35f * 35f) { Destroy(s); return true; }
            return false;
        });

        int maxSentries = w.level switch {
            1 => 1,
            2 => 1,
            3 => 2,
            4 => 2,
            _ => 3   // level 5
        };

        // Count how many sentries are currently visible on screen.
        int onScreen = 0;
        foreach (var s in _activeSentries)
            if (s != null && SurvivorMasterScript.IsOnScreen(s.transform.position)) onScreen++;

        if (onScreen >= maxSentries) return;

        // Find a random placement position around the player that is on screen.
        Vector3 spawnPos = Vector3.positiveInfinity;
        for (int attempt = 0; attempt < 15; attempt++) {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist  = Random.Range(5f, 12f);
            Vector3 candidate = playerPos + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
            if (SurvivorMasterScript.IsOnScreen(candidate)) { spawnPos = candidate; break; }
        }
        if (spawnPos == Vector3.positiveInfinity) return;

        var go = new GameObject("SentryGun");
        go.transform.position = spawnPos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;
        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        go.transform.localScale = Vector3.one * 5f;

        var logic = go.AddComponent<SentryGunLogic>();
        logic.weaponData = w;

        _activeSentries.Add(go);
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

    // Battlemage custom attack: spawns a large magic aura centred on the player,
    // animates it for its full duration, deals damage to all enemies in range every
    // tick, and applies a moderate radial knockback away from the player each hit.
    IEnumerator FireMagicAura(ItemData w) {
        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        if (frames == null || frames.Length == 0) yield break;

        int   lastFrame      = frames.Length - 1;
        float displayDuration = w.cooldown > 0f ? w.cooldown : 1.0f;
        float frameInterval  = frames.Length > 1 ? displayDuration / frames.Length : displayDuration;

        // ── Level scaling ──────────────────────────────────────────────────────
        // Lv2: aura 25% larger | Lv3: +1 dmg | Lv4: +1 knockback | Lv5: +1 dmg
        float auraScale     = 20f * (w.level >= 2 ? 1.25f : 1f);
        float knockbackForce = 2f + (w.level >= 4 ? 1f : 0f);
        float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f)
                    * (1f + RunUpgrades.DamageBonus)
                    + (w.level >= 3 ? 1f : 0f)
                    + (w.level >= 5 ? 1f : 0f);

        // ── Build aura visual ─────────────────────────────────────────────────
        var go = new GameObject("MagicAura_VFX");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 7;
        sr.sprite = frames[0];
        go.transform.localScale = Vector3.one * auraScale;
        go.transform.position   = PlayerPos;

        // ── Ring hit detection geometry ───────────────────────────────────────
        // outerRadius is derived from the sprite's actual world-space size so
        // the physics query matches the visible ring precisely.
        Sprite refSprite        = frames[0];
        float  spriteWorldSize  = Mathf.Max(refSprite.rect.width, refSprite.rect.height)
                                  / refSprite.pixelsPerUnit;
        float  outerRadius      = spriteWorldSize * auraScale * 0.5f;
        float  outerRadiusSq    = outerRadius * outerRadius;
        // innerRadius masks the transparent centre hole (~55% inward).
        float  innerRadiusSq    = (outerRadius * 0.55f) * (outerRadius * 0.55f);

        float tickRate = 0.2f;
        float nextTick = 0f;
        float elapsed  = 0f;
        int   currentFrame = 0;

        while (elapsed < displayDuration) {
            if (go == null) yield break;

            // ── Manual animation (gives us frame index for damage gating) ─────
            int newFrame = Mathf.Min((int)(elapsed / frameInterval), lastFrame);
            if (newFrame != currentFrame) {
                currentFrame = newFrame;
                sr.sprite    = frames[currentFrame];
            }

            Vector3 pos = PlayerPos;
            go.transform.position = pos;

            // ── Damage + knockback tick ───────────────────────────────────────
            // Suppressed on the first and last frames (aura is still forming/fading).
            // Enemies inside the ring band get normal knockback; enemies inside the
            // transparent centre hole (standing on the player) also get hit with
            // stronger knockback to push them out to the ring edge.
            bool canHit = currentFrame > 0 && currentFrame < lastFrame;
            if (canHit && elapsed >= nextTick) {
                nextTick += tickRate;
                foreach (Collider2D col in Physics2D.OverlapCircleAll(pos, outerRadius)) {
                    if (!col.CompareTag("Enemy")) continue;
                    var e = col.GetComponent<EnemyEntity>();
                    if (e == null || e.isDead) continue;
                    float distSq = ((Vector2)(e.transform.position - pos)).sqrMagnitude;
                    if (distSq > outerRadiusSq) continue; // outside the aura entirely
                    e.TakeDamage(dmg);
                    Vector2 knockDir = ((Vector2)(e.transform.position - pos)).normalized;
                    if (knockDir == Vector2.zero) knockDir = Vector2.right;
                    // Enemies inside the centre hole get extra knockback to eject them.
                    float force = distSq < innerRadiusSq ? knockbackForce * 2f : knockbackForce;
                    e.ApplyKnockback(knockDir, force);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // Vampire custom attack: summons a blood pool centred on the player that persists
    // for 7 seconds, damages every enemy within its radius each tick, and heals the
    // player for 25% of all damage dealt.
    IEnumerator FireBloodPool(ItemData w) {
        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        if (frames == null || frames.Length == 0) yield break;

        // ── Level scaling ──────────────────────────────────────────────────────
        // Lv2: +25% size | Lv3: +1 dmg | Lv4: 6s duration | Lv5: 50% lifesteal
        float displayDuration = w.level >= 4 ? 6f : 7f;
        float auraScale       = 37.5f * (w.level >= 2 ? 1.25f : 1f);
        float lifestealPct    = w.level >= 5 ? 0.50f : 0.25f;
        const float damageCooldown = 2f;

        int   lastFrame     = frames.Length - 1;
        float frameInterval = frames.Length > 1 ? displayDuration / frames.Length : displayDuration;

        // ── Build pool visual ─────────────────────────────────────────────────
        var go = new GameObject("BloodPool_VFX");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 2; // above floor/chunks (0) and POI (1), below gems (4), enemies (5), player (6)
        sr.sprite       = frames[0];
        go.transform.localScale = Vector3.one * auraScale;
        go.transform.position   = PlayerPos;

        // ── Hit radius: driven by w.range, independent of sprite pixelsPerUnit
        float hitRadius   = w.range > 0f ? w.range : 12f;
        float hitRadiusSq = hitRadius * hitRadius;

        float dmg = w.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f)
                    * (1f + RunUpgrades.DamageBonus)
                    + (w.level >= 3 ? 1f : 0f);
        float nextTick = 0f;
        float elapsed  = 0f;
        int   currentFrame = 0;

        while (elapsed < displayDuration) {
            if (go == null) yield break;

            // ── Keep pool centred on the player ───────────────────────────────
            Vector3 pos = PlayerPos;
            go.transform.position = pos;

            // ── Manual animation ──────────────────────────────────────────────
            int newFrame = Mathf.Min((int)(elapsed / frameInterval), lastFrame);
            if (newFrame != currentFrame) {
                currentFrame = newFrame;
                sr.sprite    = frames[currentFrame];
            }

            // ── Damage tick every 2 seconds: all enemies inside the pool radius
            if (elapsed >= nextTick) {
                nextTick += damageCooldown;
                float totalDmg = 0f;
                foreach (Collider2D col in Physics2D.OverlapCircleAll(pos, hitRadius)) {
                    if (!col.CompareTag("Enemy")) continue;
                    var e = col.GetComponent<EnemyEntity>();
                    if (e == null || e.isDead) continue;
                    float distSq = ((Vector2)(e.transform.position - pos)).sqrMagnitude;
                    if (distSq > hitRadiusSq) continue;
                    e.TakeDamage(dmg);
                    totalDmg += dmg;
                }
                if (totalDmg > 0f)
                    SurvivorMasterScript.Instance?.Heal(totalDmg * lifestealPct);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // Ranger custom attack: fires arrows at random enemies in range with level-scaled
    // bonus arrow chances.
    //
    // Level scaling:
    //   Lv1 – 1 arrow, 25% chance for a 2nd
    //   Lv2 – 2× damage, same 25% bonus chance
    //   Lv3 – 50% chance for a bonus arrow
    //   Lv4 – 100% chance for a bonus arrow (always 2 arrows minimum)
    //   Lv5 – always 2 arrows + 25% chance for a 3rd
    void FireRangerArrow(ItemData w) {
        Vector3 pos = PlayerPos;

        // Gather and shuffle targets in range (mirrors FireRandomInRange).
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        if (w.range > 0f) {
            float rangeSq = w.range * w.range;
            targets.RemoveAll(e => (e.transform.position - pos).sqrMagnitude > rangeSq);
        }
        for (int i = targets.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            var tmp = targets[i]; targets[i] = targets[j]; targets[j] = tmp;
        }

        // ── How many arrows to fire this attack? ──────────────────────────────
        int arrowCount = 1;

        if (w.level >= 5) {
            // Always 2, 25% chance for a 3rd.
            arrowCount = 2;
            if (Random.value < 0.25f) arrowCount = 3;
        } else if (w.level >= 4) {
            // 100% bonus arrow → always 2.
            arrowCount = 2;
        } else if (w.level >= 3) {
            // 50% bonus arrow.
            if (Random.value < 0.50f) arrowCount++;
        } else {
            // Lv1–2: 25% bonus arrow.
            if (Random.value < 0.25f) arrowCount++;
        }

        // ── Damage multiplier ─────────────────────────────────────────────────
        // Build a temporary copy so we don't permanently alter baseDamage.
        float originalDmg = w.baseDamage;
        if (w.level >= 2) w.baseDamage *= 2f;

        for (int i = 0; i < arrowCount; i++) {
            if (targets.Count > 0) {
                // Pick targets in order; wrap around if more arrows than targets.
                EnemyEntity t = targets[i % targets.Count];
                Vector2 dir = ((Vector2)(t.transform.position - pos)).normalized;
                SpawnProjectile(w, pos).GetComponent<ProjectileLogic>().Setup(w, dir, t);
            } else {
                // No targets in range: fire in a random spread.
                float spreadAngle = (i - (arrowCount - 1) * 0.5f) * 15f;
                Vector2 dir = Quaternion.Euler(0f, 0f, spreadAngle) * Vector2.up;
                SpawnProjectile(w, pos).GetComponent<ProjectileLogic>().Setup(w, dir);
            }
        }

        // Restore base damage so the item card isn't permanently mutated.
        w.baseDamage = originalDmg;
    }

    // Gladiator custom attack: melee stab if enemies are nearby, otherwise ranged throw.
    //
    // Melee — finds the sector with the most enemies within melee range and stabs there.
    //   Each stab extends then retracts (150 ms out / 100 ms back). Deals damage + knockback.
    //   Stab count: L1=1 | L2=2 | L4=3
    //   Damage: base × (L3: ×1.25) × (L5: ×1.50, cumulative)
    //
    // Ranged — throws trident toward nearest enemy; slows 25% on hit.
    //   Damage: base × (L2: ×1.25) × (L3: ×1.20) × (L4: ×1.25) × (L5: ×1.25, cumulative)
    //   Slow duration: 3 s (L5: 5 s)
    IEnumerator FireTridentStrike(ItemData w) {
        w.cooldown = 2.5f;

        float meleeRange = w.range > 0f ? w.range : 5f;
        Vector3 pPos     = PlayerPos;

        // Determine mode: any living enemy within melee range → melee
        bool meleeMode = false;
        foreach (var col in Physics2D.OverlapCircleAll(pPos, meleeRange)) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e != null && !e.isDead) { meleeMode = true; break; }
        }

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite   spr    = (frames != null && frames.Length > 0) ? frames[0] : null;

        if (meleeMode) {
            // ── Melee ─────────────────────────────────────────────────────────
            float meleeDmg = w.baseDamage;
            if (w.level >= 3) meleeDmg *= 1.25f;
            if (w.level >= 5) meleeDmg *= 1.50f;
            meleeDmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

            int     stabCount  = 1 + (w.level >= 2 ? 1 : 0) + (w.level >= 4 ? 1 : 0);
            Vector2 stabDir    = TridentBestDirection(pPos, meleeRange);
            float   stabAngle  = Mathf.Atan2(stabDir.y, stabDir.x) * Mathf.Rad2Deg;

            const float extendTime = 0.15f;
            const float retractTime = 0.10f;
            const float stabHitRad  = 0.8f;
            const float knockForce  = 5f;

            for (int s = 0; s < stabCount; s++) {
                var go = new GameObject("Trident_Melee_VFX");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 8;
                if (spr != null) sr.sprite = spr;
                go.transform.localScale = Vector3.one * 5f;
                go.transform.rotation   = Quaternion.Euler(0f, 0f, stabAngle - 90f);

                var hitSet = new HashSet<EnemyEntity>();

                // Extend outward
                float el = 0f;
                while (el < extendTime) {
                    var pl = SurvivorMasterScript.Instance?.player;
                    if (pl == null || go == null) { if (go != null) Destroy(go); yield break; }
                    float dist = Mathf.Lerp(0f, meleeRange, el / extendTime);
                    go.transform.position = (Vector2)pl.position + stabDir * dist;

                    foreach (var col in Physics2D.OverlapCircleAll(go.transform.position, stabHitRad)) {
                        if (!col.CompareTag("Enemy")) continue;
                        var e = col.GetComponent<EnemyEntity>();
                        if (e == null || e.isDead || hitSet.Contains(e)) continue;
                        hitSet.Add(e);
                        e.TakeDamage(meleeDmg);
                        e.ApplyKnockback(stabDir, knockForce);
                    }
                    el += Time.deltaTime;
                    yield return null;
                }

                // Retract
                el = 0f;
                while (el < retractTime) {
                    var pl = SurvivorMasterScript.Instance?.player;
                    if (pl == null || go == null) { if (go != null) Destroy(go); yield break; }
                    float dist = Mathf.Lerp(meleeRange, 0f, el / retractTime);
                    go.transform.position = (Vector2)pl.position + stabDir * dist;
                    el += Time.deltaTime;
                    yield return null;
                }

                if (go != null) Destroy(go);
            }
        } else {
            // ── Ranged ────────────────────────────────────────────────────────
            float rangedDmg = w.baseDamage;
            if (w.level >= 2) rangedDmg *= 1.25f;
            if (w.level >= 3) rangedDmg *= 1.20f;
            if (w.level >= 4) rangedDmg *= 1.25f;
            if (w.level >= 5) rangedDmg *= 1.25f;
            rangedDmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

            float slowDuration = w.level >= 5 ? 5f : 3f;

            var target = FindNearestUnstruck(pPos,
                new System.Collections.Generic.HashSet<EnemyEntity>(), 50f);
            if (target == null) yield break;

            const float throwSpeed = 14f;
            const float hitRadius  = 0.7f;

            Vector2 startPos  = (Vector2)pPos;
            Vector2 endPos    = (Vector2)target.transform.position;
            float   totalDist = Vector2.Distance(startPos, endPos);
            float   totalTime = totalDist / throwSpeed;
            // Arc height scales with distance; capped so short throws still have a visible arc.
            float arcHeight   = Mathf.Clamp(totalDist * 0.25f, 1f, 4f);

            var go = new GameObject("Trident_Ranged_VFX");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder       = 8;
            if (spr != null) sr.sprite = spr;
            go.transform.localScale = Vector3.one * 5f;
            go.transform.position   = pPos;

            // Constant horizontal velocity components for the tangent calculation.
            float hVelX = (endPos.x - startPos.x) / totalTime;
            float hVelY = (endPos.y - startPos.y) / totalTime;

            bool  landed  = false;
            float elapsed = 0f;

            while (!landed && go != null && elapsed <= totalTime) {
                float t = elapsed / totalTime;  // 0 → 1

                // Parabolic position: lerp base + vertical arc offset (world-Y axis).
                Vector2 basePos = Vector2.Lerp(startPos, endPos, t);
                float   yOffset = arcHeight * Mathf.Sin(t * Mathf.PI);
                go.transform.position = new Vector3(basePos.x, basePos.y + yOffset, 0f);

                // Tangent = horizontal velocity + derivative of the arc term.
                float arcVelY  = arcHeight * Mathf.PI / totalTime * Mathf.Cos(t * Mathf.PI);
                float angle    = Mathf.Atan2(hVelY + arcVelY, hVelX) * Mathf.Rad2Deg;
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

                foreach (var col in Physics2D.OverlapCircleAll(go.transform.position, hitRadius)) {
                    if (!col.CompareTag("Enemy")) continue;
                    var e = col.GetComponent<EnemyEntity>();
                    if (e == null || e.isDead) continue;
                    e.TakeDamage(rangedDmg);
                    e.ApplySlow(0.75f, slowDuration);
                    landed = true;
                    break;
                }

                if (!SurvivorMasterScript.IsOnScreen(go.transform.position)) break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (go != null) Destroy(go);
        }
    }

    // Divides 360° into 8 sectors of 45° and returns the center direction of the sector
    // containing the most enemies within the given range.
    Vector2 TridentBestDirection(Vector3 origin, float range) {
        int[] counts = new int[8];
        float nearestSq  = float.MaxValue;
        float nearestAng = 0f;

        foreach (var col in Physics2D.OverlapCircleAll(origin, range)) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead) continue;

            Vector2 delta = (Vector2)(col.transform.position - origin);
            float   angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            counts[Mathf.FloorToInt(angle / 45f) % 8]++;

            float sq = delta.sqrMagnitude;
            if (sq < nearestSq) { nearestSq = sq; nearestAng = angle; }
        }

        int best = 0;
        for (int i = 1; i < 8; i++)
            if (counts[i] > counts[best]) best = i;

        // Fallback when no enemies found (shouldn't happen in melee mode).
        if (counts[best] == 0) {
            float r = nearestAng * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        float centerRad = (best * 45f + 22.5f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(centerRad), Mathf.Sin(centerRad));
    }

    // Assassin custom attack: stab twice (L3: 4, L5: 6) then slash in a 45° arc (L4+: 90°).
    // All attacks are directed straight down from the player.
    // Each hit deals damage and applies poison (50% of hit dmg per tick, 2 ticks over 2 s).
    //
    // Stab: sprite extends outward 100 ms then retracts 100 ms (200 ms total per stab).
    // Slash: arc sweep over 300 ms at the same bottom position.
    // Level scaling: L2 ×1.25 dmg | L3 +2 stabs | L4 slash 90° | L5 +2 stabs + ×1.25 dmg
    IEnumerator FirePoisonDagger(ItemData w) {
        float dmg = w.baseDamage;
        if (w.level >= 2) dmg *= 1.25f;
        if (w.level >= 5) dmg *= 1.25f;
        dmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        int   stabCount  = 2 + (w.level >= 3 ? 2 : 0) + (w.level >= 5 ? 2 : 0);
        float arcDegrees = w.level >= 4 ? 90f : 45f;
        float stabLength = w.range > 0f ? w.range : 3f;

        const float stabHalf     = 0.1f;   // 100 ms out + 100 ms back = 200 ms per stab
        const float slashDur     = 0.3f;
        const float stabHitRad   = 0.6f;
        const float slashHitRad  = 0.75f;
        const float attackAngle  = 270f;   // straight down

        float attackRad = attackAngle * Mathf.Deg2Rad;
        var stabDir = new Vector2(Mathf.Cos(attackRad), Mathf.Sin(attackRad));

        Sprite[] frames = LoadWeaponSprites(w.spriteFolder);
        Sprite spr = (frames != null && frames.Length > 0) ? frames[0] : null;

        // ── Stab phase ────────────────────────────────────────────────────────
        for (int s = 0; s < stabCount; s++) {
            var go = new GameObject("PoisonStab_VFX");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;
            if (spr != null) sr.sprite = spr;
            go.transform.localScale = Vector3.one * 5f;
            go.transform.rotation   = Quaternion.Euler(0f, 0f, attackAngle - 90f);

            var hitSet = new HashSet<EnemyEntity>();

            // Extend
            float elapsed = 0f;
            while (elapsed < stabHalf) {
                var player = SurvivorMasterScript.Instance?.player;
                if (player == null || go == null) { if (go != null) Destroy(go); yield break; }
                float dist = Mathf.Lerp(0f, stabLength, elapsed / stabHalf);
                go.transform.position = (Vector2)player.position + stabDir * dist;

                foreach (var col in Physics2D.OverlapCircleAll(go.transform.position, stabHitRad)) {
                    if (!col.CompareTag("Enemy")) continue;
                    var e = col.GetComponent<EnemyEntity>();
                    if (e == null || e.isDead || hitSet.Contains(e)) continue;
                    hitSet.Add(e);
                    e.TakeDamage(dmg);
                    e.ApplyPoison(dmg * 0.5f);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Retract
            elapsed = 0f;
            while (elapsed < stabHalf) {
                var player = SurvivorMasterScript.Instance?.player;
                if (player == null || go == null) { if (go != null) Destroy(go); yield break; }
                float dist = Mathf.Lerp(stabLength, 0f, elapsed / stabHalf);
                go.transform.position = (Vector2)player.position + stabDir * dist;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        // ── Slash phase ───────────────────────────────────────────────────────
        float startAngle  = attackAngle - arcDegrees * 0.5f;
        float orbitRadius = stabLength * 0.5f;

        var slashGo = new GameObject("PoisonSlash_VFX");
        var slashSr = slashGo.AddComponent<SpriteRenderer>();
        slashSr.sortingOrder = 8;
        if (spr != null) slashSr.sprite = spr;
        slashGo.transform.localScale = Vector3.one * 5f;

        var slashHit = new HashSet<EnemyEntity>();
        float slashElapsed = 0f;

        while (slashElapsed < slashDur) {
            if (slashGo == null) yield break;
            var player = SurvivorMasterScript.Instance?.player;
            if (player == null) break;

            float t            = slashElapsed / slashDur;
            float currentAngle = startAngle + arcDegrees * t;
            float rad          = currentAngle * Mathf.Deg2Rad;
            var   outDir       = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            slashGo.transform.position = (Vector2)player.position + outDir * orbitRadius;
            slashGo.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);

            foreach (var col in Physics2D.OverlapCircleAll(slashGo.transform.position, slashHitRad)) {
                if (!col.CompareTag("Enemy")) continue;
                var e = col.GetComponent<EnemyEntity>();
                if (e == null || e.isDead || slashHit.Contains(e)) continue;
                slashHit.Add(e);
                e.TakeDamage(dmg);
                e.ApplyPoison(dmg * 0.5f);
            }

            slashElapsed += Time.deltaTime;
            yield return null;
        }

        if (slashGo != null) Destroy(slashGo);
    }
}
