using UnityEngine;

public class EnemyBullet : MonoBehaviour {
    public float        damage;
    public EnemyEntity  owner; // set by EnemyAttack when the bullet is spawned

    void OnTriggerEnter2D(Collider2D other) {
        if (other.CompareTag("Player")) {
            if (WeaponSystem.SandShieldActive) {
                // Shield blocks the bullet and reflects damage back to the shooter.
                if (owner != null && !owner.isDead)
                    owner.TakeDamage(WeaponSystem.SandShieldCounterDmg);
            } else {
                SurvivorMasterScript.Instance.TakeDamage(damage);
            }
            Destroy(gameObject);
            return;
        }
        var swarm = other.GetComponent<InsectSwarmLogic>();
        if (swarm != null && !swarm.isDead) {
            swarm.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}