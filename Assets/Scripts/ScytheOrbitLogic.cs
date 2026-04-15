using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to the scythe GameObject spawned by WeaponSystem for the NecroKnight.
/// Rotates clockwise around the player; the bottom-left corner of the scythe sprite
/// always faces the player. Damage doubles per weapon level.
/// </summary>
public class ScytheOrbitLogic : MonoBehaviour {
    public float orbitRadius = 4f;
    public float orbitSpeed  = 90f;   // degrees per second (applied clockwise)
    public float startAngle;           // current orbit angle in degrees (CCW convention)
    public float damage;

    // Minimum seconds between hits on the same enemy.
    private const float HitCooldown = 0.35f;
    private readonly Dictionary<Collider2D, float> _hitTimes = new Dictionary<Collider2D, float>();

    void Start() {
        // Kinematic Rigidbody2D is required for trigger callbacks to fire.
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Box collider shaped to match the scythe blade.
        var col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = new Vector2(1.5f, 0.6f);
    }

    void Update() {
        var player = SurvivorMasterScript.Instance?.player;
        if (player == null) return;

        // Clockwise rotation: subtract angle each frame.
        startAngle -= orbitSpeed * Time.deltaTime;

        float rad    = startAngle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
        transform.position = (Vector2)player.position + offset;

        // Rotate sprite so the bottom-left corner faces the player.
        // The local 225° direction (bottom-left) should align with the inward world direction.
        // Derivation: sprite_Z = orbitAngle - 45°
        transform.rotation = Quaternion.Euler(0f, 0f, startAngle - 45f);
    }

    void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    void OnTriggerStay2D(Collider2D other)  => TryHit(other);

    void TryHit(Collider2D other) {
        if (!other.CompareTag("Enemy")) return;

        float now = Time.time;
        if (_hitTimes.TryGetValue(other, out float last) && now - last < HitCooldown) return;
        _hitTimes[other] = now;

        var entity = other.GetComponent<EnemyEntity>();
        if (entity == null || entity.isDead) return;

        float dmg = damage
            * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f)
            * (1f + RunUpgrades.DamageBonus);
        entity.TakeDamage(dmg);
    }
}
