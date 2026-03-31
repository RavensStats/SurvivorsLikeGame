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
    }

    public void CheckSynergies() {
        // Tag Synergy Logic
        int fireCount = activeWeapons.Concat(passiveItems).Count(i => i.tags.Contains("Fire"));
        if (fireCount >= 3) StartCoroutine(InfernoAura());

        // Evolution Logic
        foreach (var r in recipes) {
            if (activeWeapons.Any(w => w.itemName == r.weaponA) && passiveItems.Any(p => p.itemName == r.itemB)) {
                Debug.Log("Evolved: " + r.resultName);
            }
        }
    }

    IEnumerator InfernoAura() { /* Damage nearby logic */ yield return null; }
    public void RapidFire(float d) { /* Sniper Ult Logic */ }
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