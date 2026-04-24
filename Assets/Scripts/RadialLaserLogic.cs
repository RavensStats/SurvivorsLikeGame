using System.Collections.Generic;
using UnityEngine;

// Projectile for the Cyborg's Radial Laser weapon.
//
// Phase 1 (Orbit): The sprite revolves 360° around the player counter-clockwise.
//   The top-right corner of the sprite faces inward (toward the player) throughout.
//   Any enemy hit by the collider takes damage (once per pass via OnTriggerEnter2D).
//   On completing the full orbit the sprite despawns and Phase 2 fires.
//
// Phase 2 (Laser): One instant-pierce red laser beam fires toward the closest enemy.
//   The visual persists for 0.75 s; damage is dealt immediately via RaycastAll.
//
// Level scaling (applied in WeaponSystem.FireRadialLaser before calling Spawn):
//   L2 – sprite scale ×1.5
//   L3 – damage ×1.5
//   L4 – cooldown ×0.5  (3 s → 1.5 s)
//   L5 – cooldown ×0.5 again  (1.5 s → 0.75 s)
public class RadialLaserLogic : MonoBehaviour {
    private float _angle;       // current orbit angle, degrees
    private float _dmg;
    private int   _beamCount;
    private bool  _done;

    private const float OrbitRadius = 2.5f;
    private const float SpinRate    = 480f;  // degrees/sec → full orbit in 0.75 s

    // Laser beam constants
    private const float LaserRange    = 25f;
    private const float LaserDuration = 0.75f;

    public static void Spawn(Vector3 playerPos, float dmg, int beamCount,
                             Sprite spr, float scale) {
        var go = new GameObject("RadialLaser");
        go.transform.position   = playerPos + new Vector3(OrbitRadius, 0f);
        go.transform.localScale = Vector3.one * scale;

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

        var logic        = go.AddComponent<RadialLaserLogic>();
        logic._dmg       = dmg;
        logic._beamCount = beamCount;

        Destroy(go, 5f); // safety timeout
    }

    void Update() {
        if (_done) return;

        var sms = SurvivorMasterScript.Instance;
        if (sms == null || sms.player == null) { Despawn(false); return; }

        Vector3 playerPos = sms.player.position;

        _angle += SpinRate * Time.deltaTime;

        float rad = _angle * Mathf.Deg2Rad;
        transform.position = playerPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad)) * OrbitRadius;

        // Top-right corner faces inward: sprite rotation = orbitAngle + 135°
        transform.rotation = Quaternion.Euler(0f, 0f, _angle + 135f);

        if (_angle >= 360f)
            Despawn(true);
    }

    void Despawn(bool fireLasers) {
        if (_done) return;
        _done = true;

        if (fireLasers) {
            var sms = SurvivorMasterScript.Instance;
            if (sms != null && sms.player != null)
                FireLasers(sms.player.position);
        }

        Destroy(gameObject);
    }

    void FireLasers(Vector3 origin) {
        var sms = SurvivorMasterScript.Instance;
        if (sms == null) return;

        var candidates = sms.Grid.GetNearby(origin);
        candidates.RemoveAll(e => e == null || e.isDead
                               || !SurvivorMasterScript.IsOnScreen(e.transform.position));
        if (candidates.Count == 0) return;

        candidates.Sort((a, b) =>
            (a.transform.position - origin).sqrMagnitude
                .CompareTo((b.transform.position - origin).sqrMagnitude));

        var targeted = new HashSet<EnemyEntity>();
        for (int i = 0; i < _beamCount && i < candidates.Count; i++) {
            var t = candidates[i];
            if (targeted.Contains(t)) continue;
            targeted.Add(t);
            SpawnLaserBeam(origin, t.transform.position, _dmg);
        }
    }

    static void SpawnLaserBeam(Vector3 origin, Vector3 targetPos, float dmg) {
        Vector2 dir = ((Vector2)(targetPos - origin)).normalized;

        var go = new GameObject("LaserBeam");
        go.transform.position = origin;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace    = true;
        lr.positionCount    = 2;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin + (Vector3)(dir * LaserRange));
        lr.startWidth       = 0.15f;
        lr.endWidth         = 0.04f;
        lr.sortingLayerName = "Default";
        lr.sortingOrder     = 9;

        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material    = mat;
        lr.startColor  = Color.red;
        lr.endColor    = new Color(1f, 0.2f, 0.2f, 0.4f);

        // Instant pierce damage to all enemies along the ray
        var hits = Physics2D.RaycastAll(origin, dir, LaserRange);
        foreach (var hit in hits) {
            if (!hit.collider.CompareTag("Enemy")) continue;
            var e = hit.collider.GetComponent<EnemyEntity>();
            if (e == null || e.isDead) continue;
            e.TakeDamage(dmg);
        }

        Destroy(go, LaserDuration);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_done || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        e.TakeDamage(_dmg);
    }
}
