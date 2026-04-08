using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to orb GameObjects spawned by WeaponSystem for the Orbit fire mode.
/// Rotates around the player and deals damage to any enemy whose collider it touches.
/// </summary>
public class OrbLogic : MonoBehaviour {
    public float orbitRadius = 3f;
    public float orbitSpeed  = 120f;   // degrees per second
    public float startAngle;           // initial angle in degrees
    public float damage;

    // Minimum seconds between hits on the same enemy (prevents per-frame overkill).
    private const float HitCooldown = 0.25f;
    private readonly Dictionary<Collider2D, float> hitTimes = new Dictionary<Collider2D, float>();

    void Start() {
        // Sprite: fall back to a procedural circle if nothing was assigned externally.
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
            sr.sprite = MakeCircleSprite(16);

        // Kinematic Rigidbody2D is required for 2D trigger callbacks to fire on this object.
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Trigger collider sized to match the visible orb.
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.5f;
    }

    void Update() {
        var player = SurvivorMasterScript.Instance?.player;
        if (player == null) return;

        startAngle += orbitSpeed * Time.deltaTime;
        float rad = startAngle * Mathf.Deg2Rad;
        transform.position = (Vector2)player.position
            + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
    }

    void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    void OnTriggerStay2D(Collider2D other)  => TryHit(other);

    void TryHit(Collider2D other) {
        if (!other.CompareTag("Enemy")) return;

        float now = Time.time;
        if (hitTimes.TryGetValue(other, out float last) && now - last < HitCooldown) return;
        hitTimes[other] = now;

        var entity = other.GetComponent<EnemyEntity>();
        if (entity == null || entity.isDead) return;
        entity.TakeDamage(damage);
    }

    static Sprite MakeCircleSprite(int res) {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float half = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++) {
                float dx = x - half, dy = y - half;
                tex.SetPixel(x, y, (dx * dx + dy * dy) <= half * half ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
