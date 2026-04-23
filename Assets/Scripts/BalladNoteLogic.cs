using UnityEngine;

// Projectile fired by the Bard's Ballad weapon.
// Travels in a sinusoidal arc at a fixed random angle.
// Deals no damage — instead charms the first enemy it touches,
// making them fight alongside the player for the charm duration.
public class BalladNoteLogic : MonoBehaviour {
    private Vector2 _baseDir;
    private Vector2 _perpDir;
    private Vector3 _origin;
    private float   _elapsed;
    private float   _charmDuration;
    private bool    _dead;

    private const float Speed     = 8f;
    private const float Amplitude = 1.5f;   // wave width in world units
    private const float Frequency = 4f;     // radians per second

    public static void Spawn(Vector3 origin, Vector2 dir, float charmDuration, Sprite spr) {
        var go = new GameObject("BalladNote");
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

        dir = dir.normalized;
        var logic = go.AddComponent<BalladNoteLogic>();
        logic._origin        = origin;
        logic._baseDir       = dir;
        logic._perpDir       = new Vector2(-dir.y, dir.x); // 90° CCW perpendicular
        logic._charmDuration = charmDuration;

        Destroy(go, 6f);
    }

    void Update() {
        _elapsed += Time.deltaTime;
        float forward = _elapsed * Speed;
        float side    = Amplitude * Mathf.Sin(_elapsed * Frequency);
        transform.position = _origin + (Vector3)(_baseDir * forward + _perpDir * side);

        if (!SurvivorMasterScript.IsOnScreen(transform.position))
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_dead || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead || e.isCharmed) return;
        _dead = true;
        e.ApplyCharm(_charmDuration);
        Destroy(gameObject);
    }
}
