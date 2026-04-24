using UnityEngine;

// Projectile for the Dwarf's War Hammer weapon.
//
// Phase 1 (Flying): The hammer arcs in a high quadratic Bezier toward the nearest enemy.
//   The sprite pivots so the top of the sprite is aligned to the flight direction at all
//   times — meaning the top lands facing the ground at impact.
//
// Phase 2 (Grounded): On impact, two expanding rings appear centered on the impact point.
//   - Small ring (light brown): marks the damage radius; enemies within take damage instantly.
//   - Large ring (lighter brown): marks the stun radius; enemies within are stunned.
//   Both rings expand at the same rate.  The small ring is hidden while the large ring has
//   not yet passed the small ring's final radius — once it does, the small ring appears.
//   After full expansion, both rings and the hammer linger for 1 second then despawn.
//
// Level scaling (applied in WeaponSystem.FireWarHammer before calling Spawn):
//   L2 – dmgRadius ×1.5, stunRadius ×1.5
//   L3 – damage ×1.5
//   L4 – stun duration 2 s (up from 1 s)
//   L5 – damage ×1.5 again
public class WarHammerLogic : MonoBehaviour {
    private enum Phase { Flying, Grounded }
    private Phase _phase = Phase.Flying;

    // Arc parameters
    private Vector3 _arcStart;
    private Vector3 _arcTarget;
    private float   _arcT;
    private float   _arcDuration;
    private float   _arcHeight;

    // Ground parameters
    private float _dmg;
    private float _stunDuration;
    private float _dmgRadius;
    private float _stunRadius;

    private float _groundTimer;
    private float _expansionR;     // current expanding radius (tracks both circles)

    // Circle visuals
    private LineRenderer _dmgLR;
    private LineRenderer _stunLR;

    private const float Speed             = 12f;
    private const float GroundDuration    = 1.0f;
    private const float ExpansionDuration = 0.35f;
    private const int   CircleSegments   = 40;

    // Light brown (damage ring, slightly darker)
    private static readonly Color DmgColor  = new Color(0.62f, 0.38f, 0.18f, 0.95f);
    // Lighter brown (stun ring)
    private static readonly Color StunColor = new Color(0.82f, 0.64f, 0.42f, 0.90f);

    public static void Spawn(Vector3 origin, Vector3 targetPos,
                             float dmg, float stunDuration,
                             float dmgRadius, float stunRadius,
                             Sprite spr, float scale) {
        var go = new GameObject("WarHammer");
        go.transform.position   = origin;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 8;
        if (spr != null) sr.sprite = spr;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic;

        float dist = Vector3.Distance(origin, targetPos);

        var logic             = go.AddComponent<WarHammerLogic>();
        logic._arcStart       = origin;
        logic._arcTarget      = targetPos;
        logic._dmg            = dmg;
        logic._stunDuration   = stunDuration;
        logic._dmgRadius      = dmgRadius;
        logic._stunRadius     = stunRadius;
        logic._arcHeight      = Mathf.Max(dist * 0.65f, 4f); // high arc
        logic._arcDuration    = Mathf.Max(dist / Speed, 0.4f);

        Destroy(go, 8f); // safety timeout
    }

    void Update() {
        if (_phase == Phase.Flying)
            UpdateFlying();
        else
            UpdateGrounded();
    }

    void UpdateFlying() {
        _arcT += Time.deltaTime / _arcDuration;
        _arcT  = Mathf.Clamp01(_arcT);

        Vector3 ctrl = (_arcStart + _arcTarget) * 0.5f + Vector3.up * _arcHeight;
        transform.position = Bezier(_arcStart, ctrl, _arcTarget, _arcT);

        // Align sprite top to flight direction so top-of-sprite lands on ground at impact
        Vector3 vel = BezierTangent(_arcStart, ctrl, _arcTarget, _arcT);
        if (vel.sqrMagnitude > 0.001f) {
            float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (_arcT >= 1f)
            Land();
    }

    void Land() {
        _phase = Phase.Grounded;
        Vector3 impactPos = transform.position;

        // Instant AoE damage (small circle)
        var hits = Physics2D.OverlapCircleAll(impactPos, _dmgRadius);
        foreach (var h in hits) {
            if (!h.CompareTag("Enemy")) continue;
            var e = h.GetComponent<EnemyEntity>();
            if (e == null || e.isDead) continue;
            e.TakeDamage(_dmg);
        }

        // Stun (large circle) — sets stun timer, movement + attack blocked by EnemyEntity/EnemyAttack
        hits = Physics2D.OverlapCircleAll(impactPos, _stunRadius);
        foreach (var h in hits) {
            if (!h.CompareTag("Enemy")) continue;
            var e = h.GetComponent<EnemyEntity>();
            if (e == null || e.isDead) continue;
            e.stun = Mathf.Max(e.stun, _stunDuration);
        }

        CreateCircleVisuals(impactPos);
        _groundTimer = GroundDuration;
    }

    void UpdateGrounded() {
        float expansionRate = _stunRadius / ExpansionDuration;
        _expansionR += expansionRate * Time.deltaTime;

        float curStun = Mathf.Min(_expansionR, _stunRadius);
        float curDmg  = Mathf.Min(_expansionR, _dmgRadius);

        if (_stunLR != null)
            UpdateCircleRadius(_stunLR, transform.position, curStun);

        if (_dmgLR != null) {
            // Reveal small ring only once large ring has expanded past the small ring's final radius
            _dmgLR.enabled = _expansionR >= _dmgRadius;
            UpdateCircleRadius(_dmgLR, transform.position, curDmg);
        }

        _groundTimer -= Time.deltaTime;
        if (_groundTimer <= 0f)
            Destroy(gameObject);
    }

    void CreateCircleVisuals(Vector3 center) {
        // Stun ring (larger, lighter brown) — always visible
        var stunGo = new GameObject("WarHammer_StunRing");
        stunGo.transform.SetParent(transform);
        _stunLR = stunGo.AddComponent<LineRenderer>();
        SetupRing(_stunLR, StunColor, 0.10f, center, 0f);

        // Damage ring (smaller, darker brown) — hidden until large ring passes it
        var dmgGo = new GameObject("WarHammer_DmgRing");
        dmgGo.transform.SetParent(transform);
        _dmgLR = dmgGo.AddComponent<LineRenderer>();
        SetupRing(_dmgLR, DmgColor, 0.13f, center, 0f);
        _dmgLR.enabled = false;
    }

    static void SetupRing(LineRenderer lr, Color color, float width, Vector3 center, float radius) {
        lr.useWorldSpace    = true;
        lr.loop             = true;
        lr.positionCount    = CircleSegments;
        lr.startWidth       = width;
        lr.endWidth         = width;
        lr.sortingLayerName = "Default";
        lr.sortingOrder     = 7;
        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material   = mat;
        lr.startColor = color;
        lr.endColor   = color;
        UpdateCircleRadius(lr, center, radius);
    }

    static void UpdateCircleRadius(LineRenderer lr, Vector3 center, float radius) {
        for (int i = 0; i < CircleSegments; i++) {
            float angle = 2f * Mathf.PI * i / CircleSegments;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    static Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
    }
}
