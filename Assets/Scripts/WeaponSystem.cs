using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WeaponSystem : MonoBehaviour {
    public static WeaponSystem Instance;
    public List<ItemData> activeWeapons, passiveItems;
    public List<ItemData> cardPool = new List<ItemData>();
    public List<EvolutionRecipe> recipes;
    private Dictionary<string, float> cooldowns = new Dictionary<string, float>();

    void Awake() => Instance = this;

    void Update() {
        if (Time.timeScale <= 0) return;
        foreach (var w in activeWeapons) {
            if (!cooldowns.ContainsKey(w.itemName)) cooldowns[w.itemName] = 0;
            cooldowns[w.itemName] -= Time.deltaTime / PersistentUpgrades.CooldownMult;
            if (cooldowns[w.itemName] <= 0) { Fire(w); cooldowns[w.itemName] = w.cooldown; }
        }
    }

    void Fire(ItemData w) {
        if (w.projectilePrefab == null) { Debug.LogWarning($"[WeaponSystem] '{w.itemName}' has no projectilePrefab assigned."); return; }
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(transform.position);
        Vector2 dir = targets.Count > 0 ? (targets[0].transform.position - transform.position).normalized : Random.insideUnitCircle.normalized;
        GameObject proj = Instantiate(w.projectilePrefab, transform.position, Quaternion.identity);
        proj.GetComponent<ProjectileLogic>().Setup(w, dir);

        // Trigger attack animation on the player animator
        SurvivorMasterScript.Instance.player?.GetComponent<PlayerAnimator>()?.TriggerFireball();
    }

    public void CheckSynergies() {
        // Tag Synergy Logic
        int fireCount = activeWeapons.Concat(passiveItems).Count(i => i.tags.Contains("Fire"));
        if (fireCount >= 3) StartCoroutine(InfernoAura());

        // Evolution Logic — swap weapon + passive into the evolved weapon
        foreach (var r in recipes) {
            ItemData weapon  = activeWeapons.Find(w => w.itemName == r.weaponA);
            ItemData passive = passiveItems.Find(p => p.itemName == r.itemB);
            if (weapon == null || passive == null) continue;

            // Avoid evolving the same combo twice
            if (activeWeapons.Exists(w => w.itemName == r.resultName)) continue;

            activeWeapons.Remove(weapon);
            passiveItems.Remove(passive);
            if (cooldowns.ContainsKey(weapon.itemName)) cooldowns.Remove(weapon.itemName);

            ItemData evolved = new ItemData {
                itemName    = r.resultName,
                description = $"Evolved form of {r.weaponA}.",
                isWeapon    = true,
                rarity      = Rarity.Legendary,
                baseDamage  = weapon.baseDamage * 2.5f,
                cooldown    = weapon.cooldown * 0.7f,
                pierceCount = weapon.pierceCount + 2,
                trait       = weapon.trait,
                tags        = new System.Collections.Generic.List<string>(weapon.tags),
                projectilePrefab = r.evolvedPrefab != null ? r.evolvedPrefab : weapon.projectilePrefab
            };
            activeWeapons.Add(evolved);
            cardPool.RemoveAll(c => c.itemName == r.weaponA || c.itemName == r.itemB);
            Debug.Log($"[WeaponSystem] Evolved {r.weaponA} + {r.itemB} → {r.resultName}");
        }
    }

    IEnumerator InfernoAura() {
        float duration = 5f;
        float tickInterval = 0.5f;
        float damagePerTick = 8f;
        float elapsed = 0f;
        while (elapsed < duration) {
            var nearby = SurvivorMasterScript.Instance.Grid.GetNearby(transform.position);
            foreach (var e in nearby)
                if (e != null) e.TakeDamage(damagePerTick);
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }

    public void RapidFire(float d) { StartCoroutine(RapidFireRoutine(d)); }
    IEnumerator RapidFireRoutine(float duration) {
        // Drain all cooldown timers so every weapon fires immediately, then
        // keep them near zero for the duration (effectively fires ~5× faster).
        var keys = new List<string>(cooldowns.Keys);
        foreach (var k in keys) cooldowns[k] = 0f;
        float elapsed = 0f;
        while (elapsed < duration) {
            foreach (var k in new List<string>(cooldowns.Keys))
                if (cooldowns[k] > 0.1f) cooldowns[k] = 0f;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}

public class ProjectileLogic : MonoBehaviour {
    private ItemData d; private Vector2 dir; private int p;
    public void Setup(ItemData item, Vector2 direction) { d = item; dir = direction; p = item.pierceCount; Destroy(gameObject, 5f); }
    void Update() => transform.Translate(dir * 12f * Time.deltaTime);
    void OnTriggerEnter2D(Collider2D other) {
        if (other.CompareTag("Enemy")) {
            other.GetComponent<EnemyEntity>().TakeDamage(d.baseDamage);
            if (d.trait == WeaponTrait.Bouncy) dir = Random.insideUnitCircle.normalized;
            p--; if (p <= 0 && d.trait != WeaponTrait.Bouncy) Destroy(gameObject);
        }
    }
}