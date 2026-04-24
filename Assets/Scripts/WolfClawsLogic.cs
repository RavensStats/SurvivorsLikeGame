using UnityEngine;

// One claw swipe in the Beastmaster's Wolf Claws attack.
//
// Two are spawned per attack, 100 ms apart.  The first claw sweeps from the
// upper-left, the second from the upper-right — both arc downward through the
// target's position, simulating a double wolf-paw strike.
//
// Damage + bleeding are applied once per swipe at the impact frame (~60% of
// the arc).  The sprite aligns to the swipe direction throughout the motion
// and the object self-destructs when the arc completes.
//
// Level scaling is applied in WeaponSystem.FireWolfClaws before calling Spawn:
//   L2 – bleed 10 s  |  L3 – damage ×1.5  |  L4 – cooldown ÷2  |  L5 – cooldown ÷3
public class WolfClawsLogic : MonoBehaviour {
    private float   _dmg;
    private float   _bleedDps;
    private float   _bleedDuration;

    private Vector3 _arcStart;
    private Vector3 _arcCtrl;
    private Vector3 _arcEnd;
    private float   _elapsed;
    private bool    _hasHit;

    private const float SWIPE_DURATION = 0.18f;  // total arc time (seconds)
    private const float HIT_FRACTION   = 0.60f;  // when along the arc damage fires
    private const float HIT_RADIUS     = 1.8f;   // world-unit radius for overlap check

    public static void Spawn(Vector3 targetPos, float dmg, float bleedDps, float bleedDuration,
                             Sprite spr, float scale, bool isSecond) {
        // isSecond drives the side — first claw comes from upper-left, second from upper-right.
        float side = isSecond ? 1f : -1f;

        Vector3 arcStart = targetPos + new Vector3(side * 1.4f,  1.8f, 0f);
        Vector3 arcEnd   = targetPos + new Vector3(-side * 0.4f, -1.0f, 0f);
        Vector3 arcCtrl  = targetPos + new Vector3(side * 0.3f,   0.4f, 0f);

        var go = new GameObject("WolfClaws_Swipe");
        go.transform.position   = arcStart;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 8;
        if (spr != null) sr.sprite = spr;

        var logic             = go.AddComponent<WolfClawsLogic>();
        logic._dmg            = dmg;
        logic._bleedDps       = bleedDps;
        logic._bleedDuration  = bleedDuration;
        logic._arcStart       = arcStart;
        logic._arcCtrl        = arcCtrl;
        logic._arcEnd         = arcEnd;

        Destroy(go, 1f); // safety timeout
    }

    void Update() {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / SWIPE_DURATION);

        transform.position = Bezier(_arcStart, _arcCtrl, _arcEnd, t);

        // Align sprite top to swipe direction so claws lead the motion.
        Vector3 tang = BezierTangent(_arcStart, _arcCtrl, _arcEnd, t);
        if (tang.sqrMagnitude > 0.001f) {
            float angle = Mathf.Atan2(tang.y, tang.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Deal damage once the swipe reaches the impact zone.
        if (!_hasHit && t >= HIT_FRACTION) {
            _hasHit = true;
            var hits = Physics2D.OverlapCircleAll(transform.position, HIT_RADIUS);
            foreach (var h in hits) {
                if (!h.CompareTag("Enemy")) continue;
                var e = h.GetComponent<EnemyEntity>();
                if (e == null || e.isDead) continue;
                e.TakeDamage(_dmg);
                e.ApplyBleed(_bleedDps, _bleedDuration);
            }
        }

        if (t >= 1f)
            Destroy(gameObject);
    }

    static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    static Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
    }
}
