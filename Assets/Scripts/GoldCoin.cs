using UnityEngine;

/// <summary>
/// Visual gold coin drop. Spawned when an enemy dies (chance-based).
/// Sweeps toward the player, adds gold to GlobalGold on collection.
/// </summary>
public class GoldCoin : MonoBehaviour {

    private int value;
    private const float CollectRadius = 2.5f;
    private const float BaseSpeed     = 4f;
    private const float Acceleration  = 8f;   // units/s² added over time
    private float speed;

    public static void Spawn(Vector3 pos, int goldValue) {
        GameObject go = new GameObject("GoldCoin");
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 18f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite s = Resources.Load<Sprite>("Sprites/GoldCoin");
        if (s != null) sr.sprite = s;
        sr.color        = new Color(1f, 0.85f, 0f); // gold yellow tint
        sr.sortingOrder = 4; // same level as gems, above floor

        GoldCoin coin  = go.AddComponent<GoldCoin>();
        coin.value = goldValue;
        coin.speed = BaseSpeed;
        Destroy(go, 15f); // auto-cleanup if never collected
    }

    void Update() {
        var g = SurvivorMasterScript.Instance;
        if (g == null || g.player == null) return;

        Vector3 playerPos = g.player.position;
        float dist = Vector3.Distance(transform.position, playerPos);

        // Sweep toward player when within collect radius
        if (dist <= CollectRadius) {
            SurvivorMasterScript.GlobalGold          += value;
            SurvivorMasterScript.Instance.totalGoldGained += value;
            Destroy(gameObject);
            return;
        }

        // Accelerate toward player once within tracking range so player can't outrun it
        speed += Acceleration * Time.deltaTime;
        if (dist < CollectRadius * 6f) {
            transform.position = Vector3.MoveTowards(
                transform.position, playerPos, speed * Time.deltaTime);
        }
    }
}
