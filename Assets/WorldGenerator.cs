using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public GameObject floorPrefab;
    public List<BiomeData> biomes;
    private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();

    void Update() {
        Vector2Int center = new Vector2Int(Mathf.FloorToInt(SurvivorMasterScript.Instance.player.position.x / 30), Mathf.FloorToInt(SurvivorMasterScript.Instance.player.position.y / 30));
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) Spawn(center + new Vector2Int(x, y));
    }

    void Spawn(Vector2Int c) {
        if (chunks.ContainsKey(c)) return;
        Vector3 p = new Vector3(c.x * 30, c.y * 30, 0);
        GameObject chunk = Instantiate(floorPrefab, p, Quaternion.identity, transform);
        chunks.Add(c, chunk);

        // Biome Logic
        float dist = p.magnitude;
        BiomeData currentBiome = dist < 200 ? biomes[0] : biomes[1];
        chunk.GetComponentInChildren<SpriteRenderer>().color = currentBiome.groundColor;
    }
}

public class POIInstance : MonoBehaviour {
    public POIType type;
    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        switch (type) {
            case POIType.Graveyard: SurvivorMasterScript.Instance.isInsideGraveyard = true; break;
            case POIType.HealingSpring: SurvivorMasterScript.Instance.playerHP = Mathf.Min(100, SurvivorMasterScript.Instance.playerHP + 25); break;
            case POIType.ManaWell: SurvivorMasterScript.Instance.ResetUltimate(); break;
        }
    }
    void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player") && type == POIType.Graveyard) SurvivorMasterScript.Instance.isInsideGraveyard = false; }
}