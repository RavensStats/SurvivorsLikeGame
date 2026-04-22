using System.Collections.Generic;
using UnityEngine;

// Projectile fired by SentryGunLogic. Moves in a straight line, deals damage and
// applies a slight knockback on the first enemy hit, then destroys itself.
public class CannonballLogic : MonoBehaviour {
    private float _dmg;
    private Vector2 _dir;
    private float _knockback;
    private bool _dead;
    private readonly HashSet<Collider2D> _hit = new HashSet<Collider2D>();

    public static void Spawn(Vector3 origin, Vector2 dir, float dmg, float knockback) {
        var go = new GameObject("SentryCannonball");
        go.transform.position = origin;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 7;
        Sprite spr = Resources.Load<Sprite>("Sprites/Weapons/Cannonball/Cannonball");
        if (spr != null) sr.sprite = spr;
        go.transform.localScale = Vector3.one * 3f;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var logic = go.AddComponent<CannonballLogic>();
        logic._dmg = dmg;
        logic._dir = dir;
        logic._knockback = knockback;

        Destroy(go, 5f);
    }

    void Update() {
        transform.Translate(_dir * 10f * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other) => HandleHit(other);
    void OnTriggerStay2D(Collider2D other) => HandleHit(other);

    void HandleHit(Collider2D other) {
        if (_dead || !other.CompareTag("Enemy") || _hit.Contains(other)) return;
        _hit.Add(other);
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        e.TakeDamage(_dmg);
        if (_knockback > 0f) e.ApplyKnockback(_dir, _knockback);
        _dead = true;
        Destroy(gameObject);
    }
}
