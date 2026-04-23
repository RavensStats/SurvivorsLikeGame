using System.Collections.Generic;
using UnityEngine;

// AoE weapon for the Gravity Manipulator.
// Spawns a purple ring at the player's position that rapidly expands to MaxRadius,
// then slightly less rapidly contracts back to zero.  Any enemy the ring frontier
// passes through takes damage and receives a force.
//
// L1-L4 (both phases): damage + pull toward center.
// L5    expand phase : damage + knockback outward.
// L5    shrink phase : damage + pull inward.
//
// Level scaling:
//   L2 – damage ×1.25
//   L3 – damage ×1.5  (cumulative: ×1.875 total)
//   L4 – cooldown ×0.8  (handled in WeaponSystem's fire call)
//   L5 – push on expand / pull on shrink (replaces pull-only)
public class GravityWellLogic : MonoBehaviour {
    private const float ExpandTime = 0.35f;
    private const float ShrinkTime = 0.65f;
    private const float PullForce  = 8f;

    private float  _dmg;
    private int    _level;
    private float  _maxRadius;
    private float  _elapsed;
    private bool   _shrinking;
    private float  _prevRadius;
    private readonly HashSet<EnemyEntity> _hitSet = new HashSet<EnemyEntity>();
    private Transform    _ring;
    private LineRenderer _lr;

    public static void Spawn(Vector3 pos, float dmg, int level, float radius) {
        var go           = new GameObject("GravityWell");
        go.transform.position = pos;
        var logic        = go.AddComponent<GravityWellLogic>();
        logic._dmg       = dmg;
        logic._level     = level;
        logic._maxRadius = radius;
        logic.BuildRing();
        Destroy(go, ExpandTime + ShrinkTime + 0.1f);
    }

    void BuildRing() {
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(transform, false);
        _ring = ringGo.transform;

        _lr = ringGo.AddComponent<LineRenderer>();
        _lr.useWorldSpace    = false;
        _lr.loop             = true;
        _lr.startWidth       = 0.2f;
        _lr.endWidth         = 0.2f;
        _lr.startColor       = new Color(0.5f, 0f, 1f, 0.9f);
        _lr.endColor         = new Color(0.5f, 0f, 1f, 0.9f);
        _lr.sortingLayerName = "Default";
        _lr.sortingOrder     = 9;
        _lr.material         = new Material(Shader.Find("Sprites/Default"));

        // Unit circle; scale the child to achieve any world-space radius cheaply.
        const int Segments = 64;
        _lr.positionCount = Segments;
        float step = 2f * Mathf.PI / Segments;
        for (int i = 0; i < Segments; i++) {
            float a = i * step;
            _lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }
        _ring.localScale = Vector3.zero;
    }

    void Update() {
        _elapsed += Time.deltaTime;

        if (!_shrinking) {
            float t          = Mathf.Clamp01(_elapsed / ExpandTime);
            float currRadius = _maxRadius * t;
            _ring.localScale = Vector3.one * currRadius;

            ApplyRingEffect(_prevRadius, currRadius, false);
            _prevRadius = currRadius;

            if (t >= 1f) {
                _shrinking  = true;
                _elapsed    = 0f;
                _prevRadius = _maxRadius;
                _hitSet.Clear(); // allow shrink ring to re-hit same enemies
            }
        } else {
            float t          = Mathf.Clamp01(_elapsed / ShrinkTime);
            float currRadius = _maxRadius * (1f - t);
            _ring.localScale = Vector3.one * currRadius;

            // Shrink frontier sweeps inward: enemies between currRadius and prevRadius.
            ApplyRingEffect(currRadius, _prevRadius, true);
            _prevRadius = currRadius;

            if (t >= 1f) Destroy(gameObject);
        }
    }

    // outerRadius is the larger bound; innerRadius is the smaller bound.
    // Hits every enemy in the annulus [innerRadius, outerRadius] once per phase.
    void ApplyRingEffect(float innerRadius, float outerRadius, bool isShrinkPhase) {
        if (outerRadius <= 0.01f) return;
        Vector2 center = transform.position;

        var hits = Physics2D.OverlapCircleAll(center, outerRadius);
        foreach (var col in hits) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead || _hitSet.Contains(e)) continue;

            float dist = Vector2.Distance(e.transform.position, center);
            if (dist < innerRadius) continue;

            _hitSet.Add(e);

            e.TakeDamage(_dmg);

            Vector2 fromCenter = ((Vector2)e.transform.position - center).normalized;
            if (_level >= 5 && !isShrinkPhase) {
                // L5 expansion: push outward
                e.ApplyKnockback(fromCenter, PullForce);
            } else {
                // L1-L4 both phases, and L5 shrink: pull toward center
                e.ApplyKnockback(-fromCenter, PullForce);
            }
        }
    }
}
