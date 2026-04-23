using System.Collections.Generic;
using UnityEngine;

// Projectile for the Druid's Creeping Vines weapon.
//
// Each vine homes toward a single reserved target enemy.
// On hit: deals damage, then spawns vinesPerNode child vines from the impact
//         position, each targeting the next closest un-hit enemy.
// The chain stops naturally once no valid targets remain.
//
// A shared HashSet<EnemyEntity> (chainExcluded) is passed by reference across all
// vines in the same firing event.  A target is added to chainExcluded at spawn time
// so sibling vines can never select the same enemy.
//
// Level scaling (set in WeaponSystem before calling Spawn):
//   L2 – damage ×1.5
//   L3 – vinesPerNode = 2  (fires 2 from player + 2 from every hit position)
//   L4 – cooldown ×0.75  (6 s)
//   L5 – cooldown ×0.75 again  (4.5 s total)
public class CreepingVinesLogic : MonoBehaviour {
    private EnemyEntity          _target;
    private float                _dmg;
    private int                  _vinesPerNode;
    private HashSet<EnemyEntity> _chainExcluded; // shared reference across the whole chain event
    private Sprite               _spr;
    private float                _scale;
    private bool                 _dead;
    private float                _traveled;

    private const float Speed    = 10f;
    private const float MaxRange = 30f; // despawn without chaining if it travels this far

    // Finds the closest valid target from origin, reserves it in chainExcluded,
    // then spawns and launches a vine toward it.
    public static void Spawn(Vector3 origin, float dmg, int vinesPerNode,
                             HashSet<EnemyEntity> chainExcluded, Sprite spr, float scale) {
        var sms = SurvivorMasterScript.Instance;
        if (sms == null) return;

        var candidates = sms.Grid.GetNearby(origin);
        candidates.RemoveAll(e => e == null || e.isDead
                               || !SurvivorMasterScript.IsOnScreen(e.transform.position)
                               || chainExcluded.Contains(e));
        if (candidates.Count == 0) return;

        // Closest enemy from origin
        EnemyEntity target   = null;
        float       bestSqDist = float.MaxValue;
        foreach (var e in candidates) {
            float d = (e.transform.position - origin).sqrMagnitude;
            if (d < bestSqDist) { bestSqDist = d; target = e; }
        }
        if (target == null) return;

        // Reserve target immediately so siblings spawned in the same loop skip it
        chainExcluded.Add(target);

        var go = new GameObject("CreepingVine");
        go.transform.position   = origin;
        go.transform.localScale = Vector3.one * scale;

        // Face the target at spawn time
        Vector2 initDir = (target.transform.position - origin).normalized;
        go.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(initDir.y, initDir.x) * Mathf.Rad2Deg);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 8;
        if (spr != null) sr.sprite = spr;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic;

        var logic = go.AddComponent<CreepingVinesLogic>();
        logic._target        = target;
        logic._dmg           = dmg;
        logic._vinesPerNode  = vinesPerNode;
        logic._chainExcluded = chainExcluded;
        logic._spr           = spr;
        logic._scale         = scale;

        Destroy(go, 12f); // safety timeout
    }

    void Update() {
        if (_dead) return;

        if (_target == null || _target.isDead
                || !SurvivorMasterScript.IsOnScreen(_target.transform.position)) {
            _dead = true;
            Destroy(gameObject);
            return;
        }

        Vector2 dir  = (_target.transform.position - transform.position).normalized;
        float   step = Speed * Time.deltaTime;
        transform.position += (Vector3)(dir * step);
        _traveled += step;

        // Keep sprite rotated toward target
        transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        if (_traveled >= MaxRange) {
            _dead = true;
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_dead || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead || e != _target) return;

        _dead = true;
        e.TakeDamage(_dmg);

        // Spawn child vines from this hit position
        Vector3 hitPos = transform.position;
        for (int i = 0; i < _vinesPerNode; i++)
            Spawn(hitPos, _dmg, _vinesPerNode, _chainExcluded, _spr, _scale);

        Destroy(gameObject);
    }
}
