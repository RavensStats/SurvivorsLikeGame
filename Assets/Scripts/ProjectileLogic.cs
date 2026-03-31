using UnityEngine;

public class ProjectileLogic : MonoBehaviour {
    private ItemData d; private Vector2 dir; private int p;
    private readonly System.Collections.Generic.HashSet<Collider2D> hit
        = new System.Collections.Generic.HashSet<Collider2D>();

    void Awake() {
        transform.localScale = new Vector3(10f, 10f, 1f);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 7;
    }

    public void Setup(ItemData item, Vector2 direction) {
        d = item; dir = direction; p = item.pierceCount; Destroy(gameObject, 5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
    void Update() => transform.Translate(dir * 12f * Time.deltaTime, Space.World);

    // OnTriggerEnter2D: projectile moves into enemy
    // OnTriggerStay2D:  projectile spawns already inside enemy (enemy too close)
    void OnTriggerEnter2D(Collider2D other) => HandleHit(other);
    void OnTriggerStay2D(Collider2D other)  => HandleHit(other);

    void HandleHit(Collider2D other) {
        if (!other.CompareTag("Enemy")) return;
        if (hit.Contains(other)) return; // already processed this collider
        hit.Add(other);
        var entity = other.GetComponent<EnemyEntity>();
        if (entity == null || entity.isDead) return;
        entity.TakeDamage(d.baseDamage);
        if (!entity.isDead && d.knockback > 0f) entity.ApplyKnockback(dir, d.knockback);
        if (d.trait == WeaponTrait.Bouncy) { dir = Random.insideUnitCircle.normalized; hit.Clear(); }
        p--; if (p <= 0 && d.trait != WeaponTrait.Bouncy) Destroy(gameObject);
    }
}
