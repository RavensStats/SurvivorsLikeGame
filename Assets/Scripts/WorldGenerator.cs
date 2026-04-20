using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public static WorldGenerator Instance { get; private set; }

    public GameObject floorPrefab;
    public List<BiomeData> biomes;
    [Tooltip("Whether distant chunks are destroyed to save memory")]
    public bool enableChunkUnloading = false;
    [Tooltip("Chunks beyond this Manhattan distance from the player are unloaded")]
    public int unloadRadius = 5;
    [Tooltip("How many chunks out to spawn in each direction (2 = 5x5 grid, 3 = 7x7 grid)")]
    public int spawnRadius = 3;
    [Tooltip("Scale of the Perlin noise used for biome blending. Higher = smaller biomes.")]
    public float biomeNoiseScale = 0.025f;
    [Tooltip("Secondary palette-shift noise scale used to balance biome frequency (helps avoid edge-biome starvation).")]
    public float biomePaletteShiftNoiseScale = 0.008f;

    // Per-run noise offset – randomised in StartGame() so every run starts in a different biome.
    private float _biomeOffsetX;
    private float _biomeOffsetY;

    private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int lastPlayerCell  = new Vector2Int(-999, -999);
    // Visual cell is offset by half a chunk so it flips at the sprite edge, not the logical origin.
    private Vector2Int _lastVisualCell = new Vector2Int(-999, -999);
    private string _lastBiomeName = null;
    private bool _gameActive = false;
    private bool _poiActive  = false; // true while player is inside a POI
    private readonly Dictionary<string, Sprite> _biomeSpriteCache = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingBiomeSpriteWarnings = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    // Expose chunk data for enemy spawner
    public IEnumerable<Vector2Int> ActiveChunkKeys => chunks.Keys;
    public Vector3 GetChunkWorldCenter(Vector2Int key) => new Vector3(key.x * 30f + 15f, key.y * 30f + 15f, 0f);
    public const float ChunkSize = 30f;

    void Awake() {
        Instance = this;
    }

    /// <summary>
    /// Resets and starts the world. Call this from OnCharacterSelected and OnPlayAgain.
    /// Clears all existing chunks, resets biome state, teleports the player to origin,
    /// and pre-warms the banner canvas so the first show has no layout delay.
    /// </summary>
    public void StartGame() {
        // Destroy all chunks from any previous run
        foreach (var kvp in chunks)
            if (kvp.Value != null) Destroy(kvp.Value);
        chunks.Clear();

        lastPlayerCell = new Vector2Int(-999, -999);
        _lastVisualCell = new Vector2Int(-999, -999);
        _lastBiomeName = null;
        _poiActive = false;

        // Randomise biome noise offset so each run starts in a different biome area.
        _biomeOffsetX = Random.Range(0f, 9000f);
        _biomeOffsetY = Random.Range(0f, 9000f);

        // Teleport player back to origin for a clean start
        if (SurvivorMasterScript.Instance != null && SurvivorMasterScript.Instance.player != null)
            SurvivorMasterScript.Instance.player.position = Vector3.zero;

        // Pre-warm the banner canvas so the first biome show has no setup delay
        BiomePOIBanner.EnsureInstance();

        _gameActive = true;
    }

    // Called by POIInstance to suppress biome banner while a POI name is being displayed.
    public void SetPoiActive(bool active) => _poiActive = active;

    // Re-shows the current biome banner; called when the player exits a POI.
    public void ShowCurrentBiome() {
        if (!string.IsNullOrEmpty(_lastBiomeName))
            BiomePOIBanner.Show(_lastBiomeName);
    }

    void Update() {
        if (!_gameActive) return;
        var playerPos = SurvivorMasterScript.Instance.player.position;

        // ── Chunk management (logical cell, exact boundary) ──────────────────
        Vector2Int currentCell = new Vector2Int(
            Mathf.FloorToInt(playerPos.x / 30f),
            Mathf.FloorToInt(playerPos.y / 30f)
        );

        if (currentCell != lastPlayerCell) {
            // Spawn chunks around the player
            for (int x = -spawnRadius; x <= spawnRadius; x++)
                for (int y = -spawnRadius; y <= spawnRadius; y++)
                    Spawn(currentCell + new Vector2Int(x, y));

            // Unload chunks that are too far away
            if (enableChunkUnloading) {
                var toRemove = new List<Vector2Int>();
                foreach (var kvp in chunks) {
                    Vector2Int delta = kvp.Key - currentCell;
                    if (Mathf.Abs(delta.x) > unloadRadius || Mathf.Abs(delta.y) > unloadRadius)
                        toRemove.Add(kvp.Key);
                }
                foreach (var key in toRemove) {
                    Destroy(chunks[key]);
                    chunks.Remove(key);
                }
            }

            lastPlayerCell = currentCell;
        }

        // ── Biome banner (visual cell, +half-chunk offset for centre-anchored sprites) ──
        if (!_poiActive) {
            Vector2Int visualCell = new Vector2Int(
                Mathf.FloorToInt((playerPos.x + 15f) / 30f),
                Mathf.FloorToInt((playerPos.y + 15f) / 30f)
            );
            if (visualCell != _lastVisualCell) {
                _lastVisualCell = visualCell;
                string newBiome = GetBiomeNameForCell(visualCell);
                if (newBiome != null && newBiome != _lastBiomeName) {
                    _lastBiomeName = newBiome;
                    BiomePOIBanner.Show(newBiome);
                }
            }
        }
    }

    void Spawn(Vector2Int c) {
        if (chunks.ContainsKey(c)) return;
        Vector3 p = new Vector3(c.x * 30, c.y * 30, 0);
        GameObject chunk = Instantiate(floorPrefab, p, Quaternion.identity, transform);
        chunks.Add(c, chunk);

        SpriteRenderer sr = chunk.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) {
            Debug.LogWarning($"[WorldGenerator] FloorChunk at {c} has no SpriteRenderer in children.", chunk);
            return;
        }
        if (sr.sprite == null) {
            Debug.LogWarning($"[WorldGenerator] SpriteRenderer on FloorChunk at {c} has no Sprite assigned.", chunk);
        }

        // Biome selection uses primary Perlin + secondary palette-shift noise.
        int biomeIndex = GetBiomeIndexForCell(c);
        BiomeData biome = biomes[biomeIndex];
        sr.color = biome.groundColor;

        // Keep the base tile tint, then draw biome texture details on top.
        SpriteRenderer overlay = GetOrCreateBiomeOverlayRenderer(chunk, sr);
        if (overlay != null) {
            overlay.sprite = LoadBiomeSprite(biome);
            overlay.color = Color.white;
            overlay.enabled = overlay.sprite != null;
        }

        // 5% chance to place a POI on this chunk
        if (Random.value < 0.05f) SpawnPOI(p + new Vector3(15f, 15f, 0f));
    }

    SpriteRenderer GetOrCreateBiomeOverlayRenderer(GameObject chunk, SpriteRenderer baseRenderer) {
        if (chunk == null || baseRenderer == null) return null;

        Transform existing = chunk.transform.Find("BiomeOverlay");
        SpriteRenderer overlay = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
        if (overlay == null) {
            GameObject go = new GameObject("BiomeOverlay");
            go.transform.SetParent(baseRenderer.transform.parent, false);
            go.transform.localPosition = baseRenderer.transform.localPosition;
            go.transform.localRotation = baseRenderer.transform.localRotation;
            go.transform.localScale = baseRenderer.transform.localScale;
            overlay = go.AddComponent<SpriteRenderer>();
        }

        overlay.sortingLayerID = baseRenderer.sortingLayerID;
        overlay.sortingOrder = baseRenderer.sortingOrder + 1;
        overlay.flipX = baseRenderer.flipX;
        overlay.flipY = baseRenderer.flipY;
        overlay.drawMode = SpriteDrawMode.Tiled;
        overlay.size = GetOverlayChunkSize(baseRenderer);

        return overlay;
    }

    Vector2 GetOverlayChunkSize(SpriteRenderer baseRenderer) {
        if (baseRenderer != null && baseRenderer.drawMode != SpriteDrawMode.Simple &&
            baseRenderer.size.x > 0.001f && baseRenderer.size.y > 0.001f)
            return baseRenderer.size;

        // Default to chunk dimensions so the biome texture repeats over the whole chunk.
        return new Vector2(ChunkSize, ChunkSize);
    }

    Sprite LoadBiomeSprite(BiomeData biome) {
        if (biome == null) return null;

        string spriteName = string.IsNullOrWhiteSpace(biome.biomeSpriteName)
            ? biome.biomeName
            : biome.biomeSpriteName;

        if (string.IsNullOrWhiteSpace(spriteName)) return null;
        string trimmedName = spriteName.Trim();

        if (_biomeSpriteCache.TryGetValue(trimmedName, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>($"Sprites/Biomes/{trimmedName}");
        if (sprite == null) sprite = Resources.Load<Sprite>($"Sprites/Biomes/{trimmedName.Replace(" ", string.Empty)}");
        if (sprite == null) sprite = Resources.Load<Sprite>($"Sprites/Biomes/{trimmedName.Replace(" ", "_")}");
        if (sprite == null) sprite = Resources.Load<Sprite>($"Sprites/Biomes/{trimmedName.Replace(" ", "-")}");

        _biomeSpriteCache[trimmedName] = sprite;

        if (sprite == null && _missingBiomeSpriteWarnings.Add(trimmedName)) {
            Debug.LogWarning($"[WorldGenerator] Missing biome sprite at Resources/Sprites/Biomes/{trimmedName}.png (Alt folder is intentionally ignored).");
        }

        return sprite;
    }

    int GetBiomeIndexForCell(Vector2 cell) {
        if (biomes == null || biomes.Count == 0) return 0;

        int biomeCount = biomes.Count;

        // Primary contiguous biome field.
        float primary = Mathf.PerlinNoise(
            (cell.x + _biomeOffsetX + 1000f) * biomeNoiseScale,
            (cell.y + _biomeOffsetY + 1000f) * biomeNoiseScale);
        int primaryIndex = Mathf.Clamp(Mathf.FloorToInt(primary * biomeCount), 0, biomeCount - 1);

        // Secondary low-frequency palette shift prevents the final list entry from
        // being starved by Perlin's naturally sparse high-end tails.
        float shiftNoise = Mathf.PerlinNoise(
            (cell.x + _biomeOffsetX + 5000f) * biomePaletteShiftNoiseScale,
            (cell.y + _biomeOffsetY + 5000f) * biomePaletteShiftNoiseScale);
        int shift = Mathf.Clamp(Mathf.FloorToInt(shiftNoise * biomeCount), 0, biomeCount - 1);

        return (primaryIndex + shift) % biomeCount;
    }

    // Returns the biome index at the given world position using the current run's noise offset.
    // EnemySpawner uses this so biome multipliers stay consistent with rendered biome colours.
    public int GetBiomeIndex(Vector3 worldPos) {
        if (biomes == null || biomes.Count == 0) return 0;
        Vector2 cell = new Vector2(worldPos.x / ChunkSize, worldPos.y / ChunkSize);
        return GetBiomeIndexForCell(cell);
    }

    // Returns the biome name for a chunk grid cell using the exact same Perlin formula as Spawn().
    string GetBiomeNameForCell(Vector2Int cell) {
        if (biomes == null || biomes.Count == 0) return null;
        int idx = GetBiomeIndexForCell(cell);
        return biomes[idx].biomeName;
    }

    void SpawnPOI(Vector3 pos) {
        const float targetWorldDiameter = 24f;

        // All 20 implemented POI types
        POIType[] all = (POIType[])System.Enum.GetValues(typeof(POIType));
        POIType chosen = all[Random.Range(0, all.Length)];

        GameObject poi = new GameObject($"POI_{chosen}");
        poi.transform.position = pos;
        poi.transform.SetParent(transform, true);

        // Visual indicator: sprite from Resources/Sprites/POI, fallback to colored circle.
        SpriteRenderer sr = poi.AddComponent<SpriteRenderer>();
        string spriteName = GetPOISpriteName(chosen);
        Sprite poiSprite = Resources.Load<Sprite>($"Sprites/POI/{spriteName}");
        if (poiSprite != null) {
            sr.sprite = poiSprite;
            sr.color = Color.white;
        } else {
            sr.sprite = MakeCircleSprite(32);
            sr.color = POIColor(chosen);
            Debug.LogWarning($"[WorldGenerator] Missing POI sprite at Resources/Sprites/POI/{spriteName}.png; using fallback circle.");
        }
        sr.sortingOrder = 1;

        // Normalize world-space icon size so different source PPUs/textures appear consistent.
        float baseDiameter = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
        float safeBaseDiameter = Mathf.Max(0.0001f, baseDiameter);
        float uniformScale = targetWorldDiameter / safeBaseDiameter;
        poi.transform.localScale = Vector3.one * uniformScale;

        POIInstance inst = poi.AddComponent<POIInstance>();
        inst.type = chosen;
    }

    static string GetPOISpriteName(POIType t) {
        switch (t) {
            case POIType.Meteorite: return "MeteorStrike";
            default: return t.ToString();
        }
    }

    static Color POIColor(POIType t) {
        switch (t) {
            case POIType.Graveyard:      return new Color(0.5f, 0.2f, 0.7f, 0.9f);
            case POIType.Forge:          return new Color(1.0f, 0.4f, 0.1f, 0.9f);
            case POIType.HolyShrine:     return new Color(1.0f, 1.0f, 0.6f, 0.9f);
            case POIType.CursedAltar:    return new Color(0.3f, 0.0f, 0.3f, 0.9f);
            case POIType.ManaWell:       return new Color(0.2f, 0.5f, 1.0f, 0.9f);
            case POIType.MerchantCart:   return new Color(1.0f, 0.85f, 0.0f, 0.9f);
            case POIType.AncientLibrary: return new Color(0.6f, 0.4f, 0.1f, 0.9f);
            case POIType.HealingSpring:  return new Color(0.2f, 0.9f, 0.4f, 0.9f);
            case POIType.ScrapHeap:      return new Color(0.5f, 0.5f, 0.5f, 0.9f);
            case POIType.VolcanicVent:   return new Color(1.0f, 0.2f, 0.0f, 0.9f);
            case POIType.FrozenObelisk:  return new Color(0.5f, 0.8f, 1.0f, 0.9f);
            case POIType.ThievesDen:     return new Color(0.7f, 0.6f, 0.1f, 0.9f);
            case POIType.Monolith:       return new Color(0.3f, 0.3f, 0.3f, 0.9f);
            case POIType.RadarStation:   return new Color(0.1f, 0.7f, 0.7f, 0.9f);
            case POIType.ToxicPit:       return new Color(0.3f, 0.7f, 0.1f, 0.9f);
            case POIType.Jungle:         return new Color(1.0f, 0.75f, 0.0f, 0.9f);
            case POIType.GoldenStatue:   return new Color(1.0f, 0.9f, 0.2f, 0.9f);
            case POIType.TimeRift:       return new Color(0.7f, 0.1f, 1.0f, 0.9f);
            case POIType.Overgrowth:     return new Color(0.0f, 0.5f, 0.1f, 0.9f);
            case POIType.Meteorite:      return new Color(0.6f, 0.3f, 0.1f, 0.9f);
            default:                     return new Color(1f, 1f, 1f, 0.9f);
        }
    }

    // Procedurally build a white filled-circle sprite of the given diameter in pixels.
    static Sprite MakeCircleSprite(int diameter) {
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = diameter * 0.5f;
        for (int y = 0; y < diameter; y++) {
            for (int x = 0; x < diameter; x++) {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy)); // anti-aliased edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), diameter);
    }
}

