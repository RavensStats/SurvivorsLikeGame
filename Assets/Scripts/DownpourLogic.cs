using UnityEngine;

// Projectile for the Hydromancer's Downpour weapon.
// Spawns above the top of the screen at a random X and falls straight down.
// Despawns on first enemy hit or when it exits the bottom of the screen.
public class DownpourLogic : MonoBehaviour {
    private float _dmg;
    private bool  _dead;
    private bool  _hasEnteredScreen;

    private const float Speed = 20f;

    public static void Spawn(Vector3 origin, float dmg, Sprite spr, float scale) {
        var go = new GameObject("Downpour_Droplet");
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 8;
        if (spr != null) sr.sprite = spr;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var logic = go.AddComponent<DownpourLogic>();
        logic._dmg = dmg;

        Destroy(go, 12f);
    }

    void Update() {
        if (_dead) return;

        transform.position += Vector3.down * Speed * Time.deltaTime;

        bool onScreen = SurvivorMasterScript.IsOnScreen(transform.position);
        if (onScreen)
            _hasEnteredScreen = true;
        else if (_hasEnteredScreen)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_dead || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        _dead = true;
        e.TakeDamage(_dmg);
        Destroy(gameObject);
    }
}
