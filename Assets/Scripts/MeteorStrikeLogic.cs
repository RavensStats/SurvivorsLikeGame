using UnityEngine;

// Projectile for the Geomancer's Meteor Strike weapon.
// Spawns just above the top of the screen at a random X position and flies
// toward a fixed target position at high speed.
//
// Sprite orientation: the bottom (impact tip) of the meteor is drawn in the
// bottom-left corner of the image, placing it at 225° in local sprite space
// (i.e. atan2(-1,-1) = -135° = 225°).  The spawn method rotates the sprite so
// that 225° aligns with the direction from spawn to target, making the tip
// point toward the enemy regardless of approach angle.
//
// On hit: damage + stun (enemy can't move for stunDuration seconds).
// Despawns on first enemy hit or after reaching the target position.
public class MeteorStrikeLogic : MonoBehaviour {
    private Vector3 _targetPos;
    private float   _dmg;
    private float   _stunDuration;
    private bool    _dead;

    private const float Speed              = 22f;
    private const float SpriteForwardAngle = 225f; // direction the tip faces at 0° rotation

    public static void Spawn(Vector3 origin, Vector3 targetPos,
                             float dmg, float stunDuration, Sprite spr) {
        var go = new GameObject("MeteorStrike");
        go.transform.position = origin;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 8;
        if (spr != null) sr.sprite = spr;
        go.transform.localScale = Vector3.one * 5f;

        // Rotate so the bottom (tip) of the meteor faces the target.
        Vector2 dir = ((Vector2)(targetPos - origin)).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle - SpriteForwardAngle);

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var logic = go.AddComponent<MeteorStrikeLogic>();
        logic._targetPos    = targetPos;
        logic._dmg          = dmg;
        logic._stunDuration = stunDuration;

        Destroy(go, 8f);
    }

    void Update() {
        if (_dead) return;

        transform.position = Vector3.MoveTowards(
            transform.position, _targetPos, Speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, _targetPos) < 0.05f)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_dead || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        _dead = true;
        e.TakeDamage(_dmg);
        e.stun = Mathf.Max(e.stun, _stunDuration);
        Destroy(gameObject);
    }
}
