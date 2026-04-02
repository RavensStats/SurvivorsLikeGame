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
    private Dictionary<string, List<GameObject>> activeOrbs = new Dictionary<string, List<GameObject>>();

    // Always use the live player position so projectiles spawn/query at the right spot.
    Vector3 PlayerPos => SurvivorMasterScript.Instance.player?.position ?? transform.position;

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
        // Non-projectile modes are handled first (no prefab needed)
        if (w.fireMode == FireMode.ArcSwing) { FireArcSwing(w); return; }
        if (w.fireMode == FireMode.Orbit)    { FireOrbit(w);    return; }

        if (w.projectilePrefab == null) { Debug.LogWarning($"[WeaponSystem] '{w.itemName}' has no projectilePrefab assigned."); return; }
        switch (w.fireMode) {
            case FireMode.NearestN:      FireNearestN(w);      break;
            case FireMode.RandomInRange: FireRandomInRange(w); break;
            default:                     FireDefault(w);       break;
        }
    }

    // Deals damage to all enemies within a 120° arc toward the nearest enemy.
    void FireArcSwing(ItemData w) {
        float range = w.range > 0f ? w.range : 3f;
        const float halfAngle = 60f;  // 120° total arc
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead);
        targets = targets.Distinct().ToList();

        // Swing toward nearest enemy; fall back to rightward direction
        Vector2 swingDir = Vector2.right;
        if (targets.Count > 0) {
            targets.Sort((a, b) =>
                (a.transform.position - pos).sqrMagnitude
                .CompareTo((b.transform.position - pos).sqrMagnitude));
            swingDir = ((Vector2)(targets[0].transform.position - pos)).normalized;
        }

        float rangeSq = range * range;
        foreach (var e in targets) {
            Vector2 toEnemy = (Vector2)(e.transform.position - pos);
            if (toEnemy.sqrMagnitude > rangeSq) continue;
            if (Vector2.Angle(swingDir, toEnemy) <= halfAngle)
                e.TakeDamage(w.baseDamage);
        }
    }

    // Spawns/maintains N orbiting objects (N = weapon level) that deal periodic burn damage.
    void FireOrbit(ItemData w) {
        if (!activeOrbs.ContainsKey(w.itemName))
            activeOrbs[w.itemName] = new List<GameObject>();

        activeOrbs[w.itemName].RemoveAll(o => o == null);

        // Orbs already match the weapon level — nothing to do
        if (activeOrbs[w.itemName].Count == w.level) return;

        foreach (var o in activeOrbs[w.itemName])
            if (o != null) Destroy(o);
        activeOrbs[w.itemName].Clear();

        for (int i = 0; i < w.level; i++) {
            var orb = new GameObject($"Orb_{w.itemName}_{i}");
            var sr = orb.AddComponent<SpriteRenderer>();
            sr.color        = new Color(1f, 0.45f, 0.05f);
            sr.sortingOrder = 6;
            var logic = orb.AddComponent<OrbLogic>();
            logic.startAngle = (360f / w.level) * i;
            logic.damage     = w.baseDamage;
            activeOrbs[w.itemName].Add(orb);
        }
    }

    void FireDefault(ItemData w) {
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        Vector2 dir = targets.Count > 0 ? (targets[0].transform.position - pos).normalized : Random.insideUnitCircle.normalized;
        Instantiate(w.projectilePrefab, pos, Quaternion.identity).GetComponent<ProjectileLogic>().Setup(w, dir);
    }

    // Fires one projectile at each of the N nearest enemies (N = weapon level).
    void FireNearestN(ItemData w) {
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead);
        // Deduplicate — SpatialGrid's 3x3 cell scan can include the same entity twice
        // if it sits on a shared boundary during an UpdateEntity call.
        targets = targets.Distinct().ToList();
        targets.Sort((a, b) =>
            (a.transform.position - pos).sqrMagnitude
            .CompareTo((b.transform.position - pos).sqrMagnitude));
        int count = Mathf.Min(w.level, targets.Count);
        if (count == 0) {
            Instantiate(w.projectilePrefab, pos, Quaternion.identity)
                .GetComponent<ProjectileLogic>().Setup(w, Random.insideUnitCircle.normalized);
            return;
        }
        for (int i = 0; i < count; i++) {
            Vector2 dir = (targets[i].transform.position - pos).normalized;
            Instantiate(w.projectilePrefab, pos, Quaternion.identity).GetComponent<ProjectileLogic>().Setup(w, dir);
        }
    }

    // Fires one projectile at each of up to N random enemies within w.range (N = weapon level).
    void FireRandomInRange(ItemData w) {
        Vector3 pos = PlayerPos;
        var targets = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        targets.RemoveAll(e => e == null || e.isDead);
        float rangeSq = w.range * w.range;
        targets = targets.Where(e => (e.transform.position - pos).sqrMagnitude <= rangeSq).ToList();
        for (int i = targets.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            var tmp = targets[i]; targets[i] = targets[j]; targets[j] = tmp;
        }
        int count = Mathf.Min(w.level, targets.Count);
        for (int i = 0; i < count; i++) {
            Vector2 dir = (targets[i].transform.position - pos).normalized;
            Instantiate(w.projectilePrefab, pos, Quaternion.identity).GetComponent<ProjectileLogic>().Setup(w, dir);
        }
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
            var nearby = SurvivorMasterScript.Instance.Grid.GetNearby(PlayerPos);
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
