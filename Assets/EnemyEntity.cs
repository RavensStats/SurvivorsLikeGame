using UnityEngine;

public class EnemyEntity : MonoBehaviour {
    public EnemyBehavior behavior;
    public float hp = 50, stun;
    public Vector2Int currentCell = new Vector2Int(-99, -99);

    void Start() {
        if (SurvivorMasterScript.Instance.nemesis.isPendingRevenge && behavior == SurvivorMasterScript.Instance.nemesis.killerType) {
            transform.localScale *= 2; hp *= 5; // Nemesis Buff
        }
    }

    void Update() {
        if (stun > 0) { stun -= Time.deltaTime; return; }
        SurvivorMasterScript.Instance.Grid.UpdateEntity(this, transform.position);
        
        Vector3 pPos = SurvivorMasterScript.Instance.player.position;
        Vector3 dir = (pPos - transform.position).normalized;
        float s = SurvivorMasterScript.Instance.isInsideGraveyard ? 5.5f : 3.5f;

        // --- Behavior Family Logic ---
        if (behavior == EnemyBehavior.Glitch || behavior == EnemyBehavior.Ghost) {
            if (Time.time % 3 < 0.02f) transform.position = pPos + (Vector3)Random.insideUnitCircle * 5f;
        } else if (behavior == EnemyBehavior.Magnet || behavior == EnemyBehavior.BlackHole) {
            SurvivorMasterScript.Instance.player.position = Vector3.MoveTowards(pPos, transform.position, 0.5f * Time.deltaTime);
        } else {
            transform.position += dir * s * Time.deltaTime; // Default Chaser
        }

        //FOR RANGER AND SNIPER, they want to maintain an ideal distance. They will move towards or away from the player to try and stay in that "Sweet Spot" range.

        // float dist = Vector3.Distance(transform.position, playerPos);
        // float idealDistance = 8f; // The distance they want to keep

        // if (dist < idealDistance - 1f) {
        //     // Too close! Back away from the player
        //     transform.position -= dir * speed * Time.deltaTime;
        // } else if (dist > idealDistance + 1f) {
        //     // Too far! Move closer to get in range
        //     transform.position += dir * speed * Time.deltaTime;
        // }
        // // If within the "Sweet Spot," they stand still and fire
    }

    public void Stun(float d) => stun = d;
    public void TakeDamage(float d) {
        var b = SurvivorMasterScript.Instance.bestiary.Find(x => x.behavior == behavior);
        if (b != null && b.isHunterBonusUnlocked) d *= 1.15f; // Bestiary Bonus applied
        hp -= d; if (hp <= 0) Die();
    }
    void Die() {
        SurvivorMasterScript.Instance.RegisterKill(behavior);
        SurvivorMasterScript.Instance.Grid.Remove(this);
        Instantiate(SurvivorMasterScript.Instance.xpGemPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}