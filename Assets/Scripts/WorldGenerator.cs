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
    [Tooltip("How many times the biome texture tiles across one chunk per axis (1 = one full image per chunk, 3 = 3×3 grid).")]
    [Range(1, 8)] public int overlayTilesPerChunk = 3;
    [Tooltip("Brightness of the biome overlay tiles (1 = full brightness, 0.75 = 25% darker).")]
    [Range(0f, 1f)] public float overlayBrightness = 0.75f;

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
    // All sprites in Resources/Sprites/Biomes/, keyed by both texture name and sprite name.
    // Built once per run so LoadBiomeSprite never has to guess the path format.
    private Dictionary<string, Sprite> _biomeSpritePool;

    // Expose chunk data for enemy spawner
    public IEnumerable<Vector2Int> ActiveChunkKeys => chunks.Keys;
    public Vector3 GetChunkWorldCenter(Vector2Int key) => new Vector3(key.x * 30f + 15f, key.y * 30f + 15f, 0f);
    public const float ChunkSize = 30f;

    void Awake() {
        Instance = this;
        // Seed offsets immediately so biome noise is never stuck at 0,0 if StartGame is delayed.
        _biomeOffsetX = Random.Range(0f, 9000f);
        _biomeOffsetY = Random.Range(0f, 9000f);
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

        // Rebuild the sprite pool and cache for the new run.
        _biomeSpriteCache.Clear();
        BuildBiomeSpritePool();
        StartCoroutine(PrewarmBiomeTextures());
    }

    IEnumerator PrewarmBiomeTextures() {
        foreach (var biome in biomes)
            LoadBiomeSprite(biome);

        // Place near the camera so the frustum culling doesn't skip the render.
        // Sorting order -9999 and near-zero alpha keep it invisible to the player.
        Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        GameObject go = new GameObject("_TexturePrewarm");
        go.transform.position = new Vector3(camPos.x, camPos.y, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = -9999;
        sr.color = new Color(1f, 1f, 1f, 0.01f);

        foreach (var biome in biomes) {
            Sprite s = LoadBiomeSprite(biome);
            if (s != null) {
                sr.sprite = s;
                yield return null;
            }
        }

        Destroy(go);
        _gameActive = true;
    }

    public string CurrentBiomeName => _lastBiomeName;

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
        // Keep floor chunks in world space so they never inherit parent movement.
        GameObject chunk = Instantiate(floorPrefab, p, Quaternion.identity);
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

        // Draw biome texture on top of the base tint as a grid of individually-rotated tiles.
        Sprite biomeSprite = LoadBiomeSprite(biome);
        if (biomeSprite != null)
            SpawnBiomeTiles(chunk, c, biomeSprite, sr);

        // 5% chance to place a POI on this chunk
        if (Random.value < 0.05f) SpawnPOI(p + new Vector3(15f, 15f, 0f));
    }

    void SpawnBiomeTiles(GameObject chunk, Vector2Int c, Sprite biomeSprite, SpriteRenderer baseRenderer) {
        float psX     = chunk.transform.lossyScale.x; // 30
        float psY     = chunk.transform.lossyScale.y; // 30
        float nativeW = biomeSprite.bounds.size.x;
        float nativeH = biomeSprite.bounds.size.y;
        int   N       = overlayTilesPerChunk;
        float tileWS  = ChunkSize / N; // tile world-space size

        // Scale so the sprite fills exactly one tile cell in world space.
        float lsX = tileWS / Mathf.Max(0.0001f, nativeW * psX);
        float lsY = tileWS / Mathf.Max(0.0001f, nativeH * psY);

        GameObject root = new GameObject("BiomeTiles");
        root.transform.SetParent(chunk.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale    = Vector3.one;

        for (int ty = 0; ty < N; ty++) {
            for (int tx = 0; tx < N; tx++) {
                GameObject go = new GameObject($"T{tx}_{ty}");
                go.transform.SetParent(root.transform, false);

                // Biome sprites have pivot (0,0) — bottom-left — confirmed in .meta files.
                // With a bottom-left pivot the transform position IS the tile's bottom-left
                // corner, so tile (tx,ty) starts at (tx/N, ty/N) in chunk-local [0,1] space,
                // placing it exactly at world (c.x*30 + tx*tileWS, c.y*30 + ty*tileWS).
                go.transform.localPosition = new Vector3((float)tx / N, (float)ty / N, 0f);
                go.transform.localScale    = new Vector3(lsX, lsY, 1f);

                // Deterministic 90° rotation that varies both across and within chunks.
                int hash = Mathf.Abs(c.x * 1637 + c.y * 2741 + tx * 419 + ty * 877);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, (hash % 4) * 90f);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite         = biomeSprite;
                float b           = overlayBrightness;
                sr.color          = new Color(b, b, b, 1f);
                sr.sortingLayerID = baseRenderer.sortingLayerID;
                sr.sortingOrder   = baseRenderer.sortingOrder + 1;
            }
        }
    }

    // Loads every sprite from Resources/Sprites/Biomes/ once and indexes them by both
    // texture file name ("Forest") and sub-sprite name ("Forest_0"), case-insensitive.
    // Called at StartGame so the pool is ready before any chunk spawns.
    void BuildBiomeSpritePool() {
        _biomeSpritePool = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
        Sprite[] all = Resources.LoadAll<Sprite>("Sprites/Biomes");
        foreach (Sprite s in all) {
            if (s == null) continue;
            // Index by sub-sprite name ("Forest_0") and, when available, by texture name ("Forest").
            if (!_biomeSpritePool.ContainsKey(s.name))
                _biomeSpritePool[s.name] = s;
            if (s.texture != null && !_biomeSpritePool.ContainsKey(s.texture.name))
                _biomeSpritePool[s.texture.name] = s;
        }
        Debug.Log($"[WorldGenerator] Biome sprite pool: {string.Join(", ", _biomeSpritePool.Keys)}");
    }

    // Returns the first pool entry whose key equals or starts with (or is a prefix of) name.
    // Handles singular/plural drift ("Mountain" ↔ "Mountains") and case differences.
    Sprite FindInPool(string name) {
        if (string.IsNullOrEmpty(name) || _biomeSpritePool == null) return null;
        if (_biomeSpritePool.TryGetValue(name, out Sprite exact)) return exact;
        foreach (var kvp in _biomeSpritePool) {
            if (kvp.Key.StartsWith(name, System.StringComparison.OrdinalIgnoreCase)
             || name.StartsWith(kvp.Key, System.StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
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

        if (_biomeSpritePool == null) BuildBiomeSpritePool();

        // Try exact match then space-separator variants.
        Sprite sprite = FindInPool(trimmedName)
            ?? FindInPool(trimmedName.Replace(" ", string.Empty))
            ?? FindInPool(trimmedName.Replace(" ", "_"))
            ?? FindInPool(trimmedName.Replace(" ", "-"));

        _biomeSpriteCache[trimmedName] = sprite;

        if (sprite == null && _missingBiomeSpriteWarnings.Add(trimmedName))
            Debug.LogWarning($"[WorldGenerator] No sprite found for biome '{trimmedName}'. " +
                $"Pool contains: [{string.Join(", ", _biomeSpritePool.Keys)}]. " +
                $"Set biomeName or biomeSpriteName to match one of those keys.");

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

    // Returns the world-space icon diameter for each POI type.
    // Adjust values here to tune individual POI visual sizes independently.
    static float GetPOIIconDiameter(POIType t) {
        switch (t) {
            case POIType.TimeRift:       return 17f;
            default:                     return 13f;
        }
    }

    void SpawnPOI(Vector3 pos) {
        const float ringWorldDiameter = 24f; // ZoneRadius * 2 – must match POIInstance.ZoneRadius

        POIType[] all = (POIType[])System.Enum.GetValues(typeof(POIType));
        POIType chosen = all[Random.Range(0, all.Length)];
        float iconWorldDiameter = GetPOIIconDiameter(chosen);

        // Root is unscaled so the icon and ring children can size independently.
        GameObject poi = new GameObject($"POI_{chosen}");
        poi.transform.position = pos;
        poi.transform.SetParent(transform, true);

        // ── Icon ─────────────────────────────────────────────────────────────
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(poi.transform, false);

        SpriteRenderer sr = iconGO.AddComponent<SpriteRenderer>();
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
        sr.sortingOrder = 3;

        float baseDiameter = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
        iconGO.transform.localScale = Vector3.one * (iconWorldDiameter / Mathf.Max(0.0001f, baseDiameter));

        // ── Zone ring ─────────────────────────────────────────────────────────
        GameObject ringGO = new GameObject("ZoneRing");
        ringGO.transform.SetParent(poi.transform, false);
        ringGO.transform.localScale = Vector3.one * ringWorldDiameter;
        SpriteRenderer ringSR = ringGO.AddComponent<SpriteRenderer>();
        ringSR.sprite = MakeRingSprite(256);
        ringSR.color = new Color(1f, 0.92f, 0.16f, 0.75f);
        ringSR.sortingOrder = 2;

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

    // Procedurally build a white ring (annulus) sprite. ringFraction controls band width as a
    // fraction of the outer radius — 0.10 gives a ring ~10% as thick as the radius.
    static Sprite MakeRingSprite(int diameter, float ringFraction = 0.10f) {
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r      = diameter * 0.5f;
        float outerR = r - 0.5f;
        float innerR = outerR * (1f - ringFraction);
        Color[] pixels = new Color[diameter * diameter];
        for (int y = 0; y < diameter; y++) {
            for (int x = 0; x < diameter; x++) {
                float dx   = x - r + 0.5f;
                float dy   = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a    = Mathf.Clamp01(outerR + 1f - dist)   // outer edge fade
                           * Mathf.Clamp01(dist - innerR);        // inner edge fade
                pixels[y * diameter + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), diameter);
    }
}

