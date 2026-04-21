using UnityEngine;
using System.Collections;

public enum AttackType { Melee, Projectile, AOE }

public class EnemyAttack : MonoBehaviour {
    public AttackType type;
    public float damage = 10f;
    public float attackInterval = 1.5f;
    
    [Header("Projectile Settings")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 8f;

    [Header("AOE Settings")]
    public float aoeRadius = 3f;
    public GameObject aoeVisualPrefab;

    private float timer;
    private EnemyEntity entity;

    void Awake() {
        entity = GetComponent<EnemyEntity>();
    }

    void Update() {
        if (entity != null && entity.isDead) return;
        timer += Time.deltaTime;
        float effectiveInterval = attackInterval * (entity != null ? entity.attackIntervalMult : 1f);
        if (timer >= effectiveInterval) {
            ExecuteAttack();
            timer = 0;
        }
    }

    void ExecuteAttack() {
        float dist = Vector3.Distance(transform.position, SurvivorMasterScript.Instance.player.position);
        float meleeRange = entity != null ? entity.attackRange : 1.5f;

        switch (type) {
            case AttackType.Melee:
                if (dist <= meleeRange) {
                    GetComponent<EnemyAnimator>()?.TriggerAttack();
                    DamagePlayer(damage);
                }
                break;

            case AttackType.Projectile:
                GetComponent<EnemyAnimator>()?.TriggerAttack();
                if (bulletPrefab != null) {
                    Vector3 dir = (SurvivorMasterScript.Instance.player.position - transform.position).normalized;
                    GameObject b = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                    b.GetComponent<Rigidbody2D>().linearVelocity = dir * bulletSpeed;
                    b.AddComponent<EnemyBullet>().damage = damage;
                } else {
                    SpawnProceduralBullet();
                }
                break;

            case AttackType.AOE:
                if (dist <= aoeRadius) {
                    GetComponent<EnemyAnimator>()?.TriggerAttack();
                    if (aoeVisualPrefab) Instantiate(aoeVisualPrefab, transform.position, Quaternion.identity);
                    DamagePlayer(damage);
                }
                break;
        }
    }

    void SpawnProceduralBullet() {
        var b   = new GameObject("EnemyBullet_Proc");
        b.tag   = "EnemyBullet";
        b.transform.position = transform.position;
        b.transform.localScale = Vector3.one * 6f;

        var sr = b.AddComponent<SpriteRenderer>();
        sr.sprite       = MakeCircleSprite(8);
        sr.color        = new Color(1f, 0.3f, 0.1f);
        sr.sortingOrder = 7;

        var col = b.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.06f;

        var rb = b.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Vector3 dir = (SurvivorMasterScript.Instance.player.position - transform.position).normalized;
        rb.linearVelocity = dir * bulletSpeed;

        var bullet = b.AddComponent<EnemyBullet>();
        bullet.damage = damage;

        Destroy(b, 6f);
    }

    static Sprite MakeCircleSprite(int res) {
        var tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float half = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++) {
                float dx = x - half, dy = y - half;
                tex.SetPixel(x, y, (dx * dx + dy * dy) <= half * half ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    void DamagePlayer(float amt) {
        float mult = entity != null ? entity.damageDealtMult : 1f;
        SurvivorMasterScript.Instance.TakeDamage(amt * mult);
    }
}