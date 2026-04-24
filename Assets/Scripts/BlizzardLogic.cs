using System.Collections.Generic;
using UnityEngine;

// One snowflake in the ArcticScout's Blizzard wave.
//
// Spawned just off the left screen edge at a random height.  Drifts right at a
// constant base speed with sinusoidal vertical oscillation and slight downward
// fall to mimic wind-blown snow.  Despawns when it exits the right screen edge.
//
// Each snowflake damages every enemy it touches once (per-flake HashSet prevents
// the same enemy being hit multiple times by a single flake).
//
// Slow values passed by FireBlizzard in WeaponSystem:
//   L1 10%  |  L2 20%  |  L3 30%  |  L4 40%  |  L5 50%  (all last 1.5 s)
public class BlizzardLogic : MonoBehaviour {
    private float                  _dmg;
    private float                  _slowMult;     // 0.9 = 10% slow … 0.5 = 50% slow
    private float                  _slowDuration;
    private float                  _speed;        // horizontal world-units / sec
    private float                  _rightEdge;    // despawn x boundary

    // Wind-drift parameters (randomised at spawn)
    private float _driftAmp;   // vertical sine amplitude (world units)
    private float _driftFreq;  // vertical sine frequency (radians / sec)
    private float _driftPhase; // per-flake phase offset
    private float _fallSpeed;  // constant downward bias (world units / sec)
    private float _spinRate;   // sprite rotation speed (deg / sec)

    private float _elapsed;

    private readonly HashSet<EnemyEntity> _hit = new HashSet<EnemyEntity>();

    public static void Spawn(Vector3 spawnPos, float rightEdge,
                             float speed, float dmg,
                             float slowMult, float slowDuration,
                             Sprite spr, float scale) {
        var go = new GameObject("Blizzard_Snowflake");
        go.transform.position   = spawnPos;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 8;
        if (spr != null) sr.sprite = spr;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.4f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic;

        var logic             = go.AddComponent<BlizzardLogic>();
        logic._dmg            = dmg;
        logic._slowMult       = slowMult;
        logic._slowDuration   = slowDuration;
        logic._speed          = speed;
        logic._rightEdge      = rightEdge;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        logic._driftAmp       = Random.Range(0.6f, 2.0f);
        logic._driftFreq      = Random.Range(0.8f, 2.2f);
        logic._driftPhase     = Random.Range(0f, Mathf.PI * 2f);
        logic._fallSpeed      = Random.Range(0.1f, 0.5f);  // slight downward drift
        logic._spinRate       = Random.Range(40f, 90f) * (Random.value < 0.5f ? 1f : -1f);

        Destroy(go, 8f); // safety timeout
    }

    void Update() {
        _elapsed += Time.deltaTime;

        // Horizontal: constant rightward speed
        float dx = _speed * Time.deltaTime;

        // Vertical: sinusoidal oscillation (wind gust) + constant downward fall
        float prevSin = _driftAmp * Mathf.Sin(_driftFreq * (_elapsed - Time.deltaTime) + _driftPhase);
        float curSin  = _driftAmp * Mathf.Sin(_driftFreq * _elapsed + _driftPhase);
        float dy = (curSin - prevSin) - _fallSpeed * Time.deltaTime;

        transform.position += new Vector3(dx, dy, 0f);

        // Slow spin for a natural tumble
        transform.Rotate(0f, 0f, _spinRate * Time.deltaTime);

        if (transform.position.x > _rightEdge)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead || _hit.Contains(e)) return;
        _hit.Add(e);
        e.TakeDamage(_dmg);
        e.ApplySlow(_slowMult, _slowDuration);
    }
}
