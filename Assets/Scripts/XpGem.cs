using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural XP gem pickup.  Spawned when an enemy dies, collected by player
/// contact, and auto-merged with nearby gems of the same tier.
///
/// Tier 1 = lowest XP (5), Tier 8 = highest XP (640).
/// Each tier doubles the previous so 2 × Tier-N merges exactly into Tier-(N+1).
///
/// Drop distribution ratchets over time: each ratchetTime seconds the weights
/// shift slightly away from low tiers and toward high tiers.
/// </summary>
public class XpGem : MonoBehaviour {

    public int tier = 1; // 1–8, default 1 so prefab clones are safe

    // ── XP awarded per tier ───────────────────────────────────────────────────
    static readonly float[] XpValues = { 5f, 10f, 20f, 40f, 80f, 160f, 320f, 640f };

    // ── Drop weight tables (unnormalized) ─────────────────────────────────────
    static readonly float[] WeightsStart = { 60f, 22f, 10f, 5f, 2f, 0.7f, 0.2f, 0.1f };
    static readonly float[] WeightsEnd   = {  1f,  2f,  5f, 12f, 20f, 30f, 20f, 10f };

    const int   MaxRatchets       = 14;
    const float MergeRadius       = 10f;
    const float MergeCheckDelay   = 60f;
    const float CollectRadius     = 2.0f;  
    const float StartingGemRadius = 10.0f;  

    // ── Global gem registry — avoids Physics2D layer/trigger-query issues ─────
    static readonly List<XpGem> AllGems = new List<XpGem>();

    // ── Per-run state ─────────────────────────────────────────────────────────
    static float ratchetTime = 120f;
    public static float PickupRadiusMultiplier = 1f; // increased by Magnetism upgrade

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    void Start() {
        // Clamp tier in case a prefab was placed with tier=0 in the Inspector
        if (tier < 1) tier = 1;
        if (tier > 8) tier = 8;
        AllGems.Add(this);
        StartCoroutine(MergeCheckRoutine());
    }

    void OnDestroy() {
        AllGems.Remove(this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public static void Init() {
        AllGems.Clear();
        PickupRadiusMultiplier = 1f;
        ratchetTime = Random.Range(90f, 150f);
        Debug.Log($"[XpGem] Ratchet period = {ratchetTime:F0}s");
    }

    public static void Spawn(Vector3 pos) => SpawnTier(PickTier(), pos);

    public static void SpawnStartingGems(Vector3 centre, int count = 3) {
        for (int i = 0; i < count; i++) {
            float angle = i * (360f / count) + Random.Range(-15f, 15f);
            float rad   = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * StartingGemRadius;
            SpawnTier(1, centre + offset);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ─────────────────────────────────────────────────────────────────────────

    static int PickTier() {
        float gameTime = SurvivorMasterScript.Instance != null ? SurvivorMasterScript.Instance.GameTime : 0f;
        float t        = Mathf.Clamp01(gameTime / (ratchetTime * MaxRatchets));

        float total = 0f;
        float[] w = new float[8];
        for (int i = 0; i < 8; i++) {
            w[i]  = Mathf.Lerp(WeightsStart[i], WeightsEnd[i], t);
            total += w[i];
        }

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        for (int i = 0; i < 8; i++) {
            acc += w[i];
            if (roll <= acc) return i + 1;
        }
        return 1;
    }

    public static void SpawnTier(int tier, Vector3 pos) {
        GameObject go = new GameObject($"XpGem_{tier}");
        go.transform.position = pos;

        // Scale up slightly per tier so higher gems are visually distinct.
        // Tier 1 = 1.0x, Tier 8 = 2.75x
        float scale = 1f;
        if(tier > 1){
            scale *= 4.0f;
        } 
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite s = Resources.Load<Sprite>($"Sprites/Gems/{tier}");
        if (s != null) sr.sprite = s;
        else Debug.LogWarning($"[XpGem] Sprite not found at Resources/Sprites/Gems/{tier}");
        sr.sortingOrder = 2;

        XpGem gem = go.AddComponent<XpGem>();
        gem.tier = tier;
        // Start() will add to AllGems and start the merge coroutine
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pickup — checked every frame, distance-based (no physics/tag dependency)
    // ─────────────────────────────────────────────────────────────────────────

    void Update() {
        var g = SurvivorMasterScript.Instance;
        if (g == null || g.player == null) return;
        if (Vector3.Distance(transform.position, g.player.position) <= CollectRadius * PickupRadiusMultiplier) {
            g.GainXP(XpValues[tier - 1]);
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Merge — uses AllGems list, no Physics2D queries
    // ─────────────────────────────────────────────────────────────────────────

    IEnumerator MergeCheckRoutine() {
        yield return new WaitForSeconds(MergeCheckDelay);
        while (this != null && gameObject != null) {
            TryMerge();
            yield return new WaitForSeconds(0.5f);
        }
    }

    void TryMerge() {
        if (tier >= 8) return;

        // Collect all live same-tier gems within range (using the registry, no Physics2D)
        var same = new List<XpGem>();
        foreach (XpGem g in AllGems) {
            if (g == null) continue;
            if (g.tier == tier && Vector3.Distance(transform.position, g.transform.position) <= MergeRadius)
                same.Add(g);
        }

        if (same.Count < 2) return; // need at least this + 1 partner

        // Lowest InstanceID is the one initiator — prevents double-merge races
        XpGem initiator = same[0];
        foreach (XpGem g in same)
            if (g.GetInstanceID() < initiator.GetInstanceID()) initiator = g;
        if (initiator != this) return;

        XpGem partner = null;
        foreach (XpGem g in same)
            if (g != this) { partner = g; break; }
        if (partner == null) return;

        Vector3 mid = (transform.position + partner.transform.position) * 0.5f;
        int newTier = tier + 1;
        Destroy(partner.gameObject);
        Destroy(gameObject);
        SpawnTier(newTier, mid);
    }
}
