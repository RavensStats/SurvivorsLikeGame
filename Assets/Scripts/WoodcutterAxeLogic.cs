using System.Collections;
using UnityEngine;

// Projectile fired by the Viking's Throwing Axe weapon.
// Travels in a straight line toward the target's position at launch time,
// spinning clockwise at a rate calculated so it arrives at exactly LandAngle
// (330°) after traveling the weapon's maximum range — ensuring the sprite
// always "plants" cleanly regardless of hit distance.
//
// On hit: deals damage, applies a 50% movement slow, then snaps to LandAngle
// and rests on the ground for 0.5 s before despawning.
// If it reaches max range without hitting anything it does the same landing.
public class WoodcutterAxeLogic : MonoBehaviour {
    private Vector3 _dir;
    private float   _dmg;
    private float   _slowDuration;
    private float   _spinRate;   // degrees/second, negative = clockwise
    private float   _maxRange;
    private float   _traveled;
    private bool    _landed;
    private bool    _hit;

    private const float Speed     = 12f;
    private const float LandAngle = 330f;
    private const int   SpinLoops = 3;   // full rotations before landing

    public static void Spawn(Vector3 origin, Vector3 targetPos,
                             float dmg, float slowDuration, float range, Sprite spr) {
        var go = new GameObject("WoodcutterAxe");
        go.transform.position = origin;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 8;
        if (spr != null) sr.sprite = spr;
        go.transform.localScale = Vector3.one * 5f;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var logic = go.AddComponent<WoodcutterAxeLogic>();
        logic._dir          = ((Vector2)(targetPos - origin)).normalized;
        logic._dmg          = dmg;
        logic._slowDuration = slowDuration;
        logic._maxRange     = range;
        // Clockwise spin rate (negative) calibrated so the axe reaches LandAngle
        // after traveling exactly MaxRange at Speed: rate = (loops*360 + land) / (range/speed)
        logic._spinRate     = -(SpinLoops * 360f + LandAngle) / (range / Speed);
    }

    void Update() {
        if (_landed || _hit) return;

        float step = Speed * Time.deltaTime;
        transform.position += _dir * step;
        _traveled += step;
        transform.Rotate(0f, 0f, _spinRate * Time.deltaTime);

        if (_traveled >= _maxRange)
            StartCoroutine(Land());
        else if (!SurvivorMasterScript.IsOnScreen(transform.position))
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_hit || _landed || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        _hit = true;
        e.TakeDamage(_dmg);
        e.ApplySlow(0.5f, _slowDuration);
        StartCoroutine(Land());
    }

    IEnumerator Land() {
        _landed = true;
        transform.rotation = Quaternion.Euler(0f, 0f, LandAngle);
        var col = GetComponent<CircleCollider2D>();
        if (col != null) col.enabled = false;
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }
}
