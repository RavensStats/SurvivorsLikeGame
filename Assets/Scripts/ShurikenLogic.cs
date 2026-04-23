using UnityEngine;

// Projectile for the Ninja's Shuriken weapon.
// Travels in a fixed direction, spinning clockwise. Despawns on first enemy hit or
// when it leaves the screen.
public class ShurikenLogic : MonoBehaviour {
    private Vector2 _dir;
    private float   _dmg;

    private const float Speed     = 12f;
    private const float SpinSpeed = -600f; // negative = clockwise in Unity

    public static void Spawn(Vector3 origin, Vector2 dir, float dmg, Sprite spr) {
        var go = new GameObject("Shuriken");
        go.transform.position = origin;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 8;
        if (spr != null) sr.sprite = spr;
        go.transform.localScale = Vector3.one * 5f;

        var col    = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic;

        var logic = go.AddComponent<ShurikenLogic>();
        logic._dir = dir.normalized;
        logic._dmg = dmg;

        Destroy(go, 10f);
    }

    void Update() {
        transform.position += (Vector3)(_dir * Speed * Time.deltaTime);
        transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);

        if (!SurvivorMasterScript.IsOnScreen(transform.position))
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        e.TakeDamage(_dmg);
        Destroy(gameObject);
    }
}
