using UnityEngine;

public class EnemyEntity : MonoBehaviour {
    public EnemyBehavior behavior;
    public float hp = 50, stun;
    public float moveSpeed = 3.5f;
    public bool  isDead = false;
    public Vector2Int currentCell = new Vector2Int(-99, -99);

    void Start() {
        if (SurvivorMasterScript.Instance.nemesis.isPendingRevenge && behavior == SurvivorMasterScript.Instance.nemesis.killerType) {
            transform.localScale *= 2; hp *= 5; // Nemesis Buff
        }
    }

    void Update() {
        if (isDead) return;
        if (stun > 0) { stun -= Time.deltaTime; return; }
        SurvivorMasterScript.Instance.Grid.UpdateEntity(this, transform.position);
        
        Vector3 pPos = SurvivorMasterScript.Instance.player.position;
        Vector3 dir = (pPos - transform.position).normalized;
        float s = moveSpeed * (SurvivorMasterScript.Instance.isInsideGraveyard ? 1.57f : 1f);

        // --- Behavior Family Logic ---
        if (behavior == EnemyBehavior.Glitch || behavior == EnemyBehavior.Ghost) {
            if (Time.time % 3 < 0.02f) transform.position = pPos + (Vector3)Random.insideUnitCircle * 5f;
        } else if (behavior == EnemyBehavior.Magnet || behavior == EnemyBehavior.BlackHole) {
            // Only pull player if this enemy is reasonably close, avoiding scene-setup accidents
            if (Vector3.Distance(transform.position, pPos) < 20f)
                SurvivorMasterScript.Instance.player.position = Vector3.MoveTowards(pPos, transform.position, 0.5f * Time.deltaTime);
        } else {
            transform.position += dir * s * Time.deltaTime; // Default Chaser
        }
    }

    public void Stun(float d) => stun = d;
    public void TakeDamage(float d) {
        if (isDead) return;
        SurvivorMasterScript.Instance.RegisterDamageDealt(d);
        var b = SurvivorMasterScript.Instance.bestiary.Find(x => x.behavior == behavior);
        if (b != null && b.isHunterBonusUnlocked) d *= 1.15f; // Bestiary Bonus applied
        hp -= d; if (hp <= 0) Die();
    }
    void Die() {
        isDead = true;
        SurvivorMasterScript.Instance.RegisterKill(behavior);
        SurvivorMasterScript.Instance.Grid.Remove(this);
        XpGem.Spawn(transform.position);
        Destroy(gameObject, 0.15f); // brief delay for death frame to show
    }
}