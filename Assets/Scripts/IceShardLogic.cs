using UnityEngine;

// Cryomancer's Ice Shard projectile — two phases:
//
// Phase 1 (Primary): A single shard travels straight toward the target.
//   On impact: deals full damage, applies 50% slow for the configured duration,
//   spawns N sub-shards fanning out behind the hit point, then despawns itself.
//
// Phase 2 (Sub-shards): Smaller shards spread in a fan (30° apart) continuing
//   through the enemy crowd.  Each deals 50% of the primary damage and applies
//   half the slow percentage, for the same duration.
//
// Level scaling applied in WeaponSystem.FireIceShard before calling Spawn:
//   L2 – slow 5 s  |  L3 – damage ×1.5  |  L4 – 5 sub-shards  |  L5 – cooldown ÷1.75
public class IceShardLogic : MonoBehaviour {
    private float   _dmg;
    private float   _slowMult;      // 0.5 = 50% slow, 0.75 = 25% slow
    private float   _slowDuration;
    private Vector3 _dir;
    private float   _speed;
    private bool    _isSubShard;
    private int     _subShardCount; // only used by primary
    private Sprite  _spr;
    private float   _scale;

    private bool _hasHit;

    private const float PRIMARY_SLOW = 0.5f;   // 50% slow on primary hit
    private const float PRIMARY_SPEED = 9f;    // world units / sec
    private const float SUB_SPEED     = 6.5f;
    private const float SPREAD_DEG    = 30f;   // angle between adjacent sub-shards

    // ── Primary shard ─────────────────────────────────────────────────────────
    public static void Spawn(Vector3 origin, Vector3 targetPos,
                             float dmg, float slowDuration, int subCount,
                             Sprite spr, float scale) {
        Vector3 dir = (targetPos - origin).normalized;
        var go = Build("IceShard_Primary", origin, dir, scale, spr);

        var logic             = go.AddComponent<IceShardLogic>();
        logic._dmg            = dmg;
        logic._slowMult       = PRIMARY_SLOW;
        logic._slowDuration   = slowDuration;
        logic._dir            = dir;
        logic._speed          = PRIMARY_SPEED;
        logic._isSubShard     = false;
        logic._subShardCount  = subCount;
        logic._spr            = spr;
        logic._scale          = scale;

        Destroy(go, 4f); // safety – full attack sequence lasts at most 4 s
    }

    // ── Sub-shard (spawned on primary impact) ────────────────────────────────
    static void SpawnSub(Vector3 origin, Vector3 dir,
                         float dmg, float slowMult, float slowDuration,
                         Sprite spr, float scale) {
        var go = Build("IceShard_Sub", origin, dir, scale * 0.8f, spr);

        var logic           = go.AddComponent<IceShardLogic>();
        logic._dmg          = dmg;
        logic._slowMult     = slowMult;
        logic._slowDuration = slowDuration;
        logic._dir          = dir;
        logic._speed        = SUB_SPEED;
        logic._isSubShard   = true;

        Destroy(go, 2.5f);
    }

    // ── Shared factory ────────────────────────────────────────────────────────
    static GameObject Build(string name, Vector3 pos, Vector3 dir, float scale, Sprite spr) {
        var go = new GameObject(name);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * scale;

        // Rotate sprite so its top (+Y local) faces the direction of travel.
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 8;
        if (spr != null) sr.sprite = spr;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.3f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic;

        return go;
    }

    // ── Movement ──────────────────────────────────────────────────────────────
    void Update() {
        transform.position += _dir * _speed * Time.deltaTime;
    }

    // ── Hit detection ─────────────────────────────────────────────────────────
    void OnTriggerEnter2D(Collider2D other) {
        if (_hasHit) return;
        if (!other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;

        _hasHit = true;
        e.TakeDamage(_dmg);
        e.ApplySlow(_slowMult, _slowDuration);

        if (!_isSubShard)
            SpawnFan(transform.position, _dir);

        Destroy(gameObject);
    }

    // ── Fan spawner ───────────────────────────────────────────────────────────
    void SpawnFan(Vector3 hitPos, Vector3 incomingDir) {
        float baseAngleDeg  = Mathf.Atan2(incomingDir.y, incomingDir.x) * Mathf.Rad2Deg;
        float totalSpread   = (_subShardCount - 1) * SPREAD_DEG;
        float subDmg        = _dmg * 0.5f;
        // Sub slow = half the slow percentage: if primary is 50% slow (mult 0.5),
        // sub is 25% slow (mult 0.75).
        float subSlowMult   = 1f - (1f - _slowMult) * 0.5f;

        for (int i = 0; i < _subShardCount; i++) {
            float angleDeg = baseAngleDeg + (-totalSpread * 0.5f + i * SPREAD_DEG);
            float rad      = angleDeg * Mathf.Deg2Rad;
            Vector3 dir    = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            SpawnSub(hitPos, dir, subDmg, subSlowMult, _slowDuration, _spr, _scale);
        }
    }
}
