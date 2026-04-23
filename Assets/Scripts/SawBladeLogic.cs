using System.Collections.Generic;
using UnityEngine;

// Projectile for the Artificer's Saw Blade weapon.
// Travels straight toward the target at launch time. On hitting an enemy it
// deals damage and deflects at a randomly-chosen ±45° angle, then keeps
// flying until it either hits another enemy (repeating the deflect) or
// leaves the screen (despawn). Each enemy can only be hit once per blade.
//
// The sprite spins continuously to sell the "spinning saw blade" look.
public class SawBladeLogic : MonoBehaviour {
    private Vector2  _dir;
    private float    _dmg;
    private bool     _dead;
    private readonly HashSet<EnemyEntity> _struck = new HashSet<EnemyEntity>();

    private const float Speed     = 15f;
    private const float SpinSpeed = -720f; // degrees/second, CW spin

    public static void Spawn(Vector3 origin, Vector2 dir, float dmg, Sprite spr) {
        var go = new GameObject("SawBlade");
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

        var logic = go.AddComponent<SawBladeLogic>();
        logic._dir = dir.normalized;
        logic._dmg = dmg;

        Destroy(go, 10f);
    }

    void Update() {
        if (_dead) return;

        transform.position += (Vector3)(_dir * Speed * Time.deltaTime);
        transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);

        if (!SurvivorMasterScript.IsOnScreen(transform.position))
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_dead || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead || _struck.Contains(e)) return;

        _struck.Add(e);
        e.TakeDamage(_dmg);

        // Deflect ±45° from the current travel direction (randomly chosen).
        float deflect     = Random.value > 0.5f ? 45f : -45f;
        float currentDeg  = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        float newRad      = (currentDeg + deflect) * Mathf.Deg2Rad;
        _dir = new Vector2(Mathf.Cos(newRad), Mathf.Sin(newRad));
    }
}
