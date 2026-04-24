using UnityEngine;

public class ProjectileLogic : MonoBehaviour {
    private ItemData d; private Vector2 dir; private int p;
    private bool _dead;
    private string _weaponName;
    private EnemyEntity _target; // when set, only this enemy can be hit
    private readonly System.Collections.Generic.HashSet<Collider2D> hit
        = new System.Collections.Generic.HashSet<Collider2D>();

    void Awake() {
        transform.localScale = new Vector3(10f, 10f, 1f);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 7;
    }

    public void Setup(ItemData item, Vector2 direction, EnemyEntity target = null) {
        d = item; _weaponName = item.itemName; dir = direction; p = item.pierceCount; _target = target; Destroy(gameObject, 5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // Arcane Arrow and Blowgun (Dart) sprites are drawn at 45° (front faces top-right), so subtract 45° to align with travel direction.
        if (item.itemName == "Arcane Arrow" || item.itemName == "Blowgun") angle -= 45f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        // Apply per-weapon scale for runtime-built projectiles (prefab instances keep their authored scale).
        if (item.projectilePrefab == null)
            transform.localScale = Vector3.one * item.projectileScale;
        // Load directional sprite from Resources when the SpriteRenderer has no sprite assigned.
        // Tries the folder-name convention first (e.g. Dart/Dart.png, Shuriken/Shuriken.png)
        // then falls back to a directional "East" sprite (e.g. Arrow/East.png).
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null && !string.IsNullOrEmpty(item.spriteFolder)) {
            string path = item.spriteFolder.Contains("/")
                ? $"Sprites/{item.spriteFolder}"
                : $"Sprites/Weapons/{item.spriteFolder}/{item.spriteFolder}";
            Sprite[] frames = Resources.LoadAll<Sprite>(path);
            if (frames == null || frames.Length == 0) {
                Sprite east = Resources.Load<Sprite>($"Sprites/Weapons/{item.spriteFolder}/East");
                frames = east != null ? new[] { east } : null;
            }
            if (frames != null && frames.Length > 0) {
                sr.sprite = frames[0];
                if (frames.Length > 1) { var anim = gameObject.AddComponent<WeaponSpriteAnimator>(); anim.Init(frames); }
            }
        }
    }
    void Update() {
        // If this arrow was locked to a specific target and that target is gone, clean up
        if (_target != null && (_target.isDead || _target.gameObject == null))
            { Destroy(gameObject); return; }
        transform.Translate(dir * 12f * Time.deltaTime, Space.World);
    }

    // OnTriggerEnter2D: projectile moves into enemy
    // OnTriggerStay2D:  projectile spawns already inside enemy (enemy too close)
    void OnTriggerEnter2D(Collider2D other) => HandleHit(other);
    void OnTriggerStay2D(Collider2D other)  => HandleHit(other);

    void HandleHit(Collider2D other) {
        if (_dead) return;
        if (!other.CompareTag("Enemy")) return;
        // If locked to a specific target, ignore all other enemies
        if (_target != null && other.GetComponent<EnemyEntity>() != _target) return;
        if (hit.Contains(other)) return; // already processed this collider
        hit.Add(other);
        var entity = other.GetComponent<EnemyEntity>();
        if (entity == null || entity.isDead) return;
        float dmg = d.baseDamage * (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
        entity.TakeDamage(dmg, weaponName: _weaponName);
        // Arcane Arrow: spawn a magic pool at the hit location (cap = weapon level).
        if (d.itemName == "Arcane Arrow")
            ArcanePool.Spawn(other.transform.position, d.level);
        // Acid Flask: drop an alchemist poison pool at the hit location, then die.
        if (d.itemName == "Acid Flask") {
            AlchemistPool.Spawn(other.transform.position, d.level, d.baseDamage);
            _dead = true; Destroy(gameObject); return;
        }
        if (!entity.isDead && d.knockback > 0f) entity.ApplyKnockback(dir, d.knockback);
        if (d.trait == WeaponTrait.Bouncy) { dir = Random.insideUnitCircle.normalized; hit.Clear(); }
        p--; if (p <= 0 && d.trait != WeaponTrait.Bouncy) { _dead = true; Destroy(gameObject); }
    }
}
