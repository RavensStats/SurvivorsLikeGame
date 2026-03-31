using UnityEngine;
using System.Collections;

public enum AttackType { Melee, Projectile, AOE }

public class EnemyAttack : MonoBehaviour {
    public AttackType type;
    public float damage = 10f;
    public float attackInterval = 1.5f;
    
    [Header("Projectile Settings")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 5f;

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
        if (timer >= attackInterval) {
            ExecuteAttack();
            timer = 0;
        }
    }

    void ExecuteAttack() {
        float dist = Vector3.Distance(transform.position, SurvivorMasterScript.Instance.player.position);

        switch (type) {
            case AttackType.Melee:
                // Only hits if touching or very close
                if (dist < 1.5f) DamagePlayer(damage);
                break;

            case AttackType.Projectile:
                // Fires a simple bullet toward player
                if (bulletPrefab) {
                    Vector3 dir = (SurvivorMasterScript.Instance.player.position - transform.position).normalized;
                    GameObject b = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                    b.GetComponent<Rigidbody2D>().linearVelocity = dir * bulletSpeed;
                    b.AddComponent<EnemyBullet>().damage = damage; // Simple helper to carry damage
                }
                break;

            case AttackType.AOE:
                // Hits everything in a circle
                if (dist <= aoeRadius) {
                    if (aoeVisualPrefab) Instantiate(aoeVisualPrefab, transform.position, Quaternion.identity);
                    DamagePlayer(damage);
                }
                break;
        }
    }

    void DamagePlayer(float amt) {
        SurvivorMasterScript.Instance.TakeDamage(amt);
    }
}