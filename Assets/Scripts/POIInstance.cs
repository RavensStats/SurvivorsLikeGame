using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to every procedurally-spawned POI tile.
/// Handles player enter/exit detection and runs each zone's per-type logic.
/// </summary>
public class POIInstance : MonoBehaviour {

    public POIType type;

    private bool _playerInside;
    private SurvivorMasterScript _sms => SurvivorMasterScript.Instance;

    // ── Per-type timers ───────────────────────────────────────────────────────
    private float _tickTimer;
    private float _radarTimer;
    private bool  _merchantUsed;
    private bool  _merchantUIShown;
    private float _timeRiftTimer;

    // ── Zone radius (matches visual scale of 24 × 0.5 col radius = 12 world units) ──
    const float ZoneRadius = 12f;

    // ─────────────────────────────────────────────────────────────────────────
    // Trigger enter / exit — called by CircleCollider2D isTrigger
    // ─────────────────────────────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        _playerInside = true;
        OnPlayerEnter();
    }

    void OnTriggerExit2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;
        OnPlayerExit();
    }

    void OnPlayerEnter() {
        BiomePOIBanner.Show(type.ToString());
        switch (type) {
            case POIType.Graveyard:
                _sms.isInsideGraveyard = true;
                if (EnemySpawner.Instance != null)
                    EnemySpawner.Instance.GlobalSpawnRateMult *= 2f;
                XpGem.TierBonus++;
                break;

            case POIType.Forge:
                _sms.poiDamageMult += 0.10f;
                break;

            case POIType.HolyShrine:
                StartCoroutine(HolyShrineRoutine());
                break;

            case POIType.CursedAltar:
                _sms.poiExtraCards++;
                StartCoroutine(CursedAltarRoutine());
                break;

            case POIType.ManaWell:
                _sms.poiCooldownMult *= (1f / 0.85f); // 15% faster (divide interval by 0.85 to reduce it)
                break;

            case POIType.MerchantCart:
                if (!_merchantUsed && !_merchantUIShown) {
                    _merchantUIShown = true;
                    StartCoroutine(MerchantCartPrompt());
                }
                break;

            case POIType.AncientLibrary:
                XpGem.PickupRadiusMultiplier = Mathf.Max(XpGem.PickupRadiusMultiplier, 4f);
                break;

            case POIType.HealingSpring:
                _sms.EnableRegen(2f, 2f);
                break;

            case POIType.ScrapHeap:
                StartCoroutine(ScrapHeapRoutine());
                break;

            case POIType.VolcanicVent:
                StartCoroutine(VolcanicVentRoutine());
                break;

            case POIType.FrozenObelisk:
                StartCoroutine(FrozenObeliskRoutine());
                break;

            case POIType.ThievesDen:
                _sms.poiGoldMult = Mathf.Max(_sms.poiGoldMult, 2f);
                break;

            case POIType.Monolith:
                _sms.poiBlockKnockback = true;
                break;

            case POIType.RadarStation:
                if (_radarTimer <= 0f) StartCoroutine(RadarStationRoutine());
                break;

            case POIType.ToxicPit:
                _sms.poiHalfSpeed = true;
                StartCoroutine(ToxicPitRoutine());
                break;

            case POIType.Beehive:
                StartCoroutine(BeehiveRoutine());
                break;

            case POIType.GoldenStatue:
                _sms.poiGoldMult = 3f;
                break;

            case POIType.TimeRift:
                // Handled in Update
                break;

            case POIType.Overgrowth:
                StartCoroutine(OvergrowthRoutine());
                break;

            case POIType.Meteorite:
                StartCoroutine(MeteoriteRoutine());
                break;
        }
    }

    void OnPlayerExit() {
        switch (type) {
            case POIType.Graveyard:
                _sms.isInsideGraveyard = false;
                if (EnemySpawner.Instance != null)
                    EnemySpawner.Instance.GlobalSpawnRateMult = Mathf.Max(1f, EnemySpawner.Instance.GlobalSpawnRateMult / 2f);
                XpGem.TierBonus = Mathf.Max(0, XpGem.TierBonus - 1);
                break;

            case POIType.Forge:
                _sms.poiDamageMult -= 0.10f;
                if (_sms.poiDamageMult < 1f) _sms.poiDamageMult = 1f;
                break;

            case POIType.HolyShrine:
                StopAllCoroutines();
                break;

            case POIType.CursedAltar:
                _sms.poiExtraCards = Mathf.Max(0, _sms.poiExtraCards - 1);
                StopAllCoroutines();
                break;

            case POIType.ManaWell:
                _sms.poiCooldownMult *= 0.85f;
                if (_sms.poiCooldownMult > 1f) _sms.poiCooldownMult = 1f;
                break;

            case POIType.AncientLibrary:
                if (XpGem.PickupRadiusMultiplier >= 4f) XpGem.PickupRadiusMultiplier = 1f;
                break;

            case POIType.HealingSpring:
                _sms.EnableRegen(-2f, 2f); // undo
                break;

            case POIType.ScrapHeap:
                StopAllCoroutines();
                break;

            case POIType.VolcanicVent:
                StopAllCoroutines();
                break;

            case POIType.FrozenObelisk:
                StopAllCoroutines();
                break;

            case POIType.ThievesDen:
                if (_sms.poiGoldMult >= 2f) _sms.poiGoldMult = 1f;
                break;

            case POIType.Monolith:
                _sms.poiBlockKnockback = false;
                break;

            case POIType.ToxicPit:
                _sms.poiHalfSpeed = false;
                StopAllCoroutines();
                break;

            case POIType.Beehive:
                StopAllCoroutines();
                break;

            case POIType.GoldenStatue:
                if (_sms.poiGoldMult >= 3f) _sms.poiGoldMult = 1f;
                break;

            case POIType.TimeRift:
                Time.timeScale = 1f;
                break;

            case POIType.Overgrowth:
                StopAllCoroutines();
                break;

            case POIType.Meteorite:
                StopAllCoroutines();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update — Time Rift fluctuation (runs regardless of player inside)
    // ─────────────────────────────────────────────────────────────────────────
    void Update() {
        if (_playerInside && type == POIType.TimeRift) {
            float t = Mathf.PingPong(Time.unscaledTime * 0.25f, 1f);
            Time.timeScale = Mathf.Lerp(0.5f, 1.5f, t);
        }
    }

    void OnDestroy() {
        // Safety: restore any state if POI is destroyed while player is inside
        if (_playerInside) OnPlayerExit();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-type coroutines
    // ─────────────────────────────────────────────────────────────────────────

    // Holy Shrine — every 5s destroy all enemy projectiles in zone
    IEnumerator HolyShrineRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(5f);
            foreach (var bullet in Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None)) {
                if (Vector3.Distance(bullet.transform.position, transform.position) <= ZoneRadius)
                    Destroy(bullet.gameObject);
            }
        }
    }

    // Cursed Altar — drain 1 HP every 5s while inside
    IEnumerator CursedAltarRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(5f);
            _sms.TakeDamage(1f);
        }
    }

    // Merchant Cart — one-time prompt to buy a level-up
    IEnumerator MerchantCartPrompt() {
        // Wait a frame so the player is fully inside before any UI shows
        yield return null;
        int xpCost = 50;
        if (SurvivorMasterScript.GlobalGold >= xpCost) {
            SurvivorMasterScript.GlobalGold -= xpCost;
            _sms.GainXP(_sms.xpMax); // force a level-up
            _merchantUsed = true;
        }
        _merchantUIShown = false;
    }

    // Scrap Heap — every 5s spawn a magnet that pulls all XP gems to player
    IEnumerator ScrapHeapRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(5f);
            PullAllGems();
        }
    }

    void PullAllGems() {
        if (_sms == null || _sms.player == null) return;
        Vector3 pPos = _sms.player.position;
        // Find all XpGem components in the scene and move them toward the player
        foreach (var gem in Object.FindObjectsByType<XpGem>(FindObjectsSortMode.None))
            StartCoroutine(SweepGem(gem, pPos));
    }

    IEnumerator SweepGem(XpGem gem, Vector3 target) {
        float t = 0f;
        Vector3 start = gem.transform.position;
        while (t < 1f && gem != null) {
            t += Time.deltaTime * 3f;
            gem.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
    }

    // Volcanic Vent — every 3s spawn two random fireballs in zone
    IEnumerator VolcanicVentRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(3f);
            for (int i = 0; i < 2; i++) {
                Vector3 spawnPos = transform.position + (Vector3)Random.insideUnitCircle * ZoneRadius;
                SpawnFireball(spawnPos);
            }
        }
    }

    void SpawnFireball(Vector3 pos) {
        var go = new GameObject("Fireball");
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 5f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.color        = new Color(1f, 0.4f, 0.05f);
        sr.sortingOrder = 8;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.5f;
        go.AddComponent<FireballHazard>();
        Destroy(go, 3f);
    }

    // Frozen Obelisk — every 0.5s slow all enemies within zone by 50%
    IEnumerator FrozenObeliskRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(0.5f);
            foreach (var e in _sms.Grid.GetNearby(transform.position)) {
                if (e == null || e.isDead) continue;
                if (Vector3.Distance(e.transform.position, transform.position) <= ZoneRadius)
                    e.moveSpeed = Mathf.Max(e.moveSpeed * 0.5f, 0.3f);
            }
        }
    }

    // Radar Station — stand for 60s to trigger airstrike
    IEnumerator RadarStationRoutine() {
        _radarTimer = 60f;
        while (_playerInside && _radarTimer > 0f) {
            _radarTimer -= Time.deltaTime;
            yield return null;
        }
        if (_radarTimer <= 0f) StartCoroutine(AirStrike());
    }

    IEnumerator AirStrike() {
        // Three strikes across a random horizontal strip near the player
        Vector3 centre = _sms.player.position;
        for (int i = 0; i < 8; i++) {
            Vector3 strikePos = centre + new Vector3(Random.Range(-20f, 20f), 0f, 0f);
            yield return new WaitForSeconds(0.2f);
            // Create brief explosion flash
            var vfx = new GameObject("AirStrikeHit");
            vfx.transform.position = strikePos;
            vfx.transform.localScale = Vector3.one * 10f;
            var sr = vfx.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.7f, 0f, 0.9f);
            sr.sortingOrder = 15;
            Destroy(vfx, 0.3f);
            // Damage enemies in blast
            foreach (var e in _sms.Grid.GetNearby(strikePos))
                if (e != null && !e.isDead && Vector3.Distance(e.transform.position, strikePos) < 3f)
                    e.TakeDamage(40f);
        }
    }

    // Toxic Pit — every second deal 5 DOT to nearby enemies; player speed halved by poiHalfSpeed
    IEnumerator ToxicPitRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(1f);
            foreach (var e in _sms.Grid.GetNearby(transform.position)) {
                if (e == null || e.isDead) continue;
                if (Vector3.Distance(e.transform.position, transform.position) <= ZoneRadius)
                    e.TakeDamage(5f);
            }
        }
    }

    // Beehive — every 4s spawn a neutral bee that damages anything on contact
    IEnumerator BeehiveRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(4f);
            SpawnBee(transform.position + (Vector3)Random.insideUnitCircle * 3f);
        }
    }

    void SpawnBee(Vector3 pos) {
        var go = new GameObject("Bee");
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 3f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.85f, 0f);
        sr.sortingOrder = 8;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.bodyType = RigidbodyType2D.Dynamic;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        go.AddComponent<BeeHazard>();
        // Random wander direction
        rb.linearVelocity = Random.insideUnitCircle.normalized * 6f;
        Destroy(go, 8f);
    }

    // Overgrowth — every 4s stun all enemies in zone for 2s
    IEnumerator OvergrowthRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(4f);
            foreach (var e in _sms.Grid.GetNearby(transform.position)) {
                if (e == null || e.isDead) continue;
                if (Vector3.Distance(e.transform.position, transform.position) <= ZoneRadius)
                    e.Stun(2f);
            }
        }
    }

    // Meteorite — every 8s a large strike on a random area near the player
    IEnumerator MeteoriteRoutine() {
        while (_playerInside) {
            yield return new WaitForSeconds(8f);
            Vector3 target = _sms.player.position + (Vector3)Random.insideUnitCircle * 15f;
            StartCoroutine(MeteorImpact(target));
        }
    }

    IEnumerator MeteorImpact(Vector3 target) {
        // Brief warning flash
        var warn = new GameObject("MeteorWarning");
        warn.transform.position   = target;
        warn.transform.localScale = Vector3.one * 16f;
        var ws = warn.AddComponent<SpriteRenderer>();
        ws.color = new Color(1f, 0.3f, 0.1f, 0.35f);
        ws.sortingOrder = 7;
        Destroy(warn, 1.2f);

        yield return new WaitForSeconds(1.2f);

        // Impact
        var vfx = new GameObject("MeteorImpact");
        vfx.transform.position = target;
        vfx.transform.localScale = Vector3.one * 20f;
        var vs = vfx.AddComponent<SpriteRenderer>();
        vs.color = new Color(1f, 0.6f, 0.1f, 1f);
        vs.sortingOrder = 15;
        Destroy(vfx, 0.4f);

        // Damage everything
        if (Vector3.Distance(target, _sms.player.position) < 5f) _sms.TakeDamage(20f);
        foreach (var e in _sms.Grid.GetNearby(target))
            if (e != null && !e.isDead && Vector3.Distance(e.transform.position, target) < 5f)
                e.TakeDamage(50f);
    }
}

// ─── Hazard components ────────────────────────────────────────────────────────

/// <summary>Fireball spawned by Volcanic Vent. Damages both player and enemies on touch.</summary>
public class FireballHazard : MonoBehaviour {
    private readonly HashSet<Collider2D> _hit = new HashSet<Collider2D>();

    void OnTriggerEnter2D(Collider2D other) {
        if (_hit.Contains(other)) return;
        _hit.Add(other);
        if (other.CompareTag("Player"))
            SurvivorMasterScript.Instance.TakeDamage(8f);
        else if (other.CompareTag("Enemy")) {
            var e = other.GetComponent<EnemyEntity>();
            if (e != null && !e.isDead) e.TakeDamage(12f);
        }
    }
}

/// <summary>Neutral bee spawned by Beehive. Damages both player and enemies on touch, then dies.</summary>
public class BeeHazard : MonoBehaviour {
    void OnTriggerEnter2D(Collider2D other) {
        if (other.CompareTag("Player")) {
            SurvivorMasterScript.Instance.TakeDamage(4f);
            Destroy(gameObject);
        } else if (other.CompareTag("Enemy")) {
            var e = other.GetComponent<EnemyEntity>();
            if (e != null && !e.isDead) { e.TakeDamage(8f); Destroy(gameObject); }
        }
    }
}
