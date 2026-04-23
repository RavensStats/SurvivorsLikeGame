using UnityEngine;

// Projectile for the Berserker's Axe weapon.
//
// Phase 1 (Throw): flies in a parabolic arc toward a random enemy.
//   On hit: deals damage (with optional crit), then begins return.
//   On reaching target without a hit: begins return from target position.
//
// Phase 2 (Return): flies in a parabolic arc back toward the live player position.
//   On arrival: despawns and calls WeaponSystem.OnBerserkerAxeReturned() so the
//   weapon can fire again immediately.
//
// Damage formula (applied at throw time, passed in as _dmg):
//   baseDamage × (1 + missingHpFraction) × (1.5 at L4+) × poiMult × damageBonus
// Crit: L2 = 10 %, L3 = 25 % total — doubles the hit damage.
public class BerserkerAxeLogic : MonoBehaviour {
    private enum Phase { Throw, Return }

    private Phase   _phase = Phase.Throw;

    // Throw arc
    private Vector3 _throwStart;
    private Vector3 _throwTarget;
    private float   _throwT;
    private float   _throwDuration;
    private float   _arcHeight;

    // Return arc
    private Vector3 _returnStart;
    private float   _returnT;
    private float   _returnDuration;
    private float   _returnArcHeight;

    private float   _dmg;
    private float   _critChance;
    private bool    _hitProcessed; // true once damage has been dealt or axe missed
    private bool    _done;

    private const float Speed    = 15f;   // world-units per second
    private const float SpinRate = -540f; // degrees/second, clockwise

    public static void Spawn(Vector3 origin, Vector3 targetPos,
                             float dmg, float critChance, Sprite spr, float scale) {
        var go = new GameObject("BerserkerAxe");
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 8;
        if (spr != null) sr.sprite = spr;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        float dist = Vector3.Distance(origin, targetPos);

        var logic = go.AddComponent<BerserkerAxeLogic>();
        logic._throwStart    = origin;
        logic._throwTarget   = targetPos;
        logic._dmg           = dmg;
        logic._critChance    = critChance;
        logic._arcHeight     = dist * 0.35f;
        logic._throwDuration = Mathf.Max(dist / Speed, 0.1f);

        Destroy(go, 20f); // safety timeout
    }

    void Update() {
        if (_done) return;
        transform.Rotate(0f, 0f, SpinRate * Time.deltaTime);

        if (_phase == Phase.Throw)
            UpdateThrow();
        else
            UpdateReturn();
    }

    void UpdateThrow() {
        _throwT += Time.deltaTime / _throwDuration;
        _throwT  = Mathf.Clamp01(_throwT);

        Vector3 ctrl    = (_throwStart + _throwTarget) * 0.5f + Vector3.up * _arcHeight;
        transform.position = Bezier(_throwStart, ctrl, _throwTarget, _throwT);

        if (_throwT >= 1f) BeginReturn();
    }

    void BeginReturn() {
        _phase           = Phase.Return;
        _hitProcessed    = true;
        _returnStart     = transform.position;
        _returnT         = 0f;

        Vector3 playerPos  = SurvivorMasterScript.Instance?.player?.position ?? _returnStart;
        float   dist       = Vector3.Distance(_returnStart, playerPos);
        _returnArcHeight   = dist * 0.35f;
        _returnDuration    = Mathf.Max(dist / Speed, 0.3f);

        var col = GetComponent<CircleCollider2D>();
        if (col != null) col.enabled = false;
    }

    void UpdateReturn() {
        _returnT += Time.deltaTime / _returnDuration;
        _returnT  = Mathf.Clamp01(_returnT);

        Vector3 playerPos = SurvivorMasterScript.Instance?.player?.position ?? _returnStart;
        Vector3 flat      = Vector3.Lerp(_returnStart, playerPos, _returnT);
        float   arc       = Mathf.Sin(Mathf.PI * _returnT) * _returnArcHeight;
        transform.position = flat + Vector3.up * arc;

        if (_returnT >= 1f) Despawn();
    }

    void Despawn() {
        if (_done) return;
        _done = true;
        WeaponSystem.Instance?.OnBerserkerAxeReturned();
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_hitProcessed || _phase != Phase.Throw || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        _hitProcessed = true;

        float finalDmg = _dmg;
        if (_critChance > 0f && Random.value < _critChance)
            finalDmg *= 2f;

        e.TakeDamage(finalDmg);
        BeginReturn();
    }

    static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }
}
