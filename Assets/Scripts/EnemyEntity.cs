using UnityEngine;

public class EnemyEntity : MonoBehaviour {
    public EnemyBehavior behavior;
    public float hp = 50, stun;
    public float moveSpeed = 3.5f;
    public bool  isDead = false;
    public Vector2Int currentCell = new Vector2Int(-99, -99);

    // Behavior-specific timers
    private float chargeTimer    = 0f;
    private float shamHealTimer  = 0f;
    private bool  isCharging     = false;
    private Vector3 chargeDir;

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
            if (Vector3.Distance(transform.position, pPos) < 20f)
                SurvivorMasterScript.Instance.player.position = Vector3.MoveTowards(pPos, transform.position, 0.5f * Time.deltaTime);
        } else if (behavior == EnemyBehavior.Ranged) {
            // Keep ~9 units away from player; orbit if within 6 units
            float dist = Vector3.Distance(transform.position, pPos);
            if (dist < 6f) {
                // Too close — back away
                transform.position += -dir * s * Time.deltaTime;
            } else if (dist > 12f) {
                // Too far — close in slowly
                transform.position += dir * (s * 0.5f) * Time.deltaTime;
            } else {
                // Preferred range — strafe perpendicular
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                transform.position += perp * s * Time.deltaTime;
            }
        } else if (behavior == EnemyBehavior.Charger) {
            chargeTimer -= Time.deltaTime;
            if (isCharging) {
                transform.position += chargeDir * s * 3.5f * Time.deltaTime;
                if (chargeTimer <= 0f) { isCharging = false; chargeTimer = 3f; }
            } else {
                // Walk toward player normally while cooling down
                transform.position += dir * s * Time.deltaTime;
                if (chargeTimer <= 0f) {
                    // Trigger a dash
                    isCharging = true;
                    chargeDir  = dir;
                    chargeTimer = 0.4f; // dash lasts 0.4s
                }
            }
        } else if (behavior == EnemyBehavior.Exploder) {
            transform.position += dir * s * 1.4f * Time.deltaTime;
            // Explode on contact
            if (Vector3.Distance(transform.position, pPos) < 1.5f) {
                SurvivorMasterScript.Instance.TakeDamage(hp * 0.5f); // big hit
                TakeDamage(9999f); // self-destruct
            }
        } else if (behavior == EnemyBehavior.Shaman) {
            // Move toward player at reduced speed
            transform.position += dir * s * 0.6f * Time.deltaTime;
            // Heal nearby enemies every 5s
            shamHealTimer -= Time.deltaTime;
            if (shamHealTimer <= 0f) {
                shamHealTimer = 5f;
                var nearby = SurvivorMasterScript.Instance.Grid.GetNearby(transform.position);
                foreach (var e in nearby)
                    if (e != null && !e.isDead && e != this) e.hp += 10f;
            }
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

        // 15% chance to drop 1 gold, rare enemies drop 2-5
        if (Random.value < 0.15f) {
            int goldDrop = Random.Range(1, 3);
            SurvivorMasterScript.GlobalGold          += goldDrop;
            SurvivorMasterScript.Instance.totalGoldGained += goldDrop;
        }

        Destroy(gameObject, 0.15f); // brief delay for death frame to show
    }
}