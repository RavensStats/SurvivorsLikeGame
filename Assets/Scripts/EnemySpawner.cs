using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for a single enemy type in the spawner.
/// Set up in the Inspector on the EnemySpawner component.
/// </summary>
[System.Serializable]
public class EnemySpawnConfig {
    [Tooltip("Folder name in Resources/Sprites/Enemies/ (e.g. \"Creeper\")")]
    public string enemyTypeName = "Creeper";

    [Tooltip("EnemyBehavior enum value assigned to this enemy")]
    public EnemyBehavior behavior = EnemyBehavior.Chaser;

    [Header("Stats")]
    public float hp           = 50f;
    public float moveSpeed    = 3.5f;
    public float damage       = 10f;
    public float attackRange  = 1.0f;
    public float attackInterval = 1.5f;

    [Header("Wave Settings")]
    [Tooltip("Number of enemies spawned per wave trigger")]
    public int   countPerWave   = 3;
    [Tooltip("Delay between individual spawns in a single wave")]
    public float spawnInterval  = 0.4f;
    [Tooltip("Time between wave triggers")]
    public float waveInterval   = 8f;
    [Tooltip("First wave delay after the run starts")]
    public float initialDelay   = 3f;

    [Header("Group Spawn")]
    [Tooltip("When true all enemies in a wave spawn in a tight cluster at one location")]
    public bool  groupSpawn     = false;
    [Tooltip("Radius of the cluster when groupSpawn is true")]
    public float groupRadius    = 2.5f;

    [Header("Scaling (optional)")]
    [Tooltip("Additional HP added per elapsed game-minute")]
    public float hpScalePerMinute     = 5f;
    [Tooltip("Additional damage added per elapsed game-minute")]
    public float damageScalePerMinute = 1f;
}

/// <summary>
/// Spawns enemies in configurable waves on already-generated floor chunks.
/// Attach to any persistent GameObject in the scene.
/// Each enemy type gets its own independent wave cycle running in a coroutine.
/// </summary>
public class EnemySpawner : MonoBehaviour {

    public static EnemySpawner Instance { get; private set; }

    [Tooltip("List of enemy types and their wave configurations")]
    public List<EnemySpawnConfig> enemyTypes = new List<EnemySpawnConfig> {
        new EnemySpawnConfig()   // default Creeper entry — edit in Inspector
    };

    [Tooltip("Minimum distance from the player that an enemy can spawn")]
    public float minSpawnDistFromPlayer = 12f;

    private WorldGenerator worldGen;
    private bool running = false;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    void Awake() {
        Instance = this;
    }

    void Start() {
        worldGen = Object.FindFirstObjectByType<WorldGenerator>();
        // Auto-start if the game is already running (bypasses character-select flow)
        if (Time.timeScale > 0f) StartSpawning();
    }

    // ── Public control ─────────────────────────────────────────────────────────

    /// <summary>Begin all wave coroutines. Called from MainMenuManager when a run starts.</summary>
    public void StartSpawning() {
        if (running) return;
        running = true;
        foreach (var cfg in enemyTypes)
            StartCoroutine(WaveLoop(cfg));
    }

    /// <summary>Stop all waves and destroy all living enemies.</summary>
    public void StopSpawning() {
        running = false;
        StopAllCoroutines();
        foreach (var e in Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None))
            Destroy(e.gameObject);
    }

    // ── Wave coroutine ─────────────────────────────────────────────────────────
    IEnumerator WaveLoop(EnemySpawnConfig cfg) {
        yield return new WaitForSeconds(cfg.initialDelay);

        while (running) {
            yield return SpawnWave(cfg);
            yield return new WaitForSeconds(cfg.waveInterval);
        }
    }

    IEnumerator SpawnWave(EnemySpawnConfig cfg) {
        // Pick a single base spawn position for the whole wave
        Vector3 basePos = PickSpawnPosition();

        float minutes = SurvivorMasterScript.Instance != null
            ? SurvivorMasterScript.Instance.GameTime / 60f
            : 0f;

        float scaledHP  = cfg.hp     + cfg.hpScalePerMinute     * minutes;
        float scaledDmg = cfg.damage + cfg.damageScalePerMinute  * minutes;

        for (int i = 0; i < cfg.countPerWave; i++) {
            if (!running) yield break;

            Vector3 spawnPos = cfg.groupSpawn
                ? basePos + (Vector3)(Random.insideUnitCircle * cfg.groupRadius)
                : PickSpawnPosition();

            SpawnEnemy(cfg, spawnPos, scaledHP, scaledDmg);

            if (i < cfg.countPerWave - 1)
                yield return new WaitForSeconds(cfg.spawnInterval);
        }
    }

    // ── Single enemy creation ──────────────────────────────────────────────────
    void SpawnEnemy(EnemySpawnConfig cfg, Vector3 pos, float hp, float damage) {
        // Apply biome multipliers at the spawn position
        float biomeHPMult     = 1f;
        float biomeDmgMult    = 1f;
        float biomeSpeedMult  = 1f;
        if (worldGen != null && worldGen.biomes != null && worldGen.biomes.Count > 0) {
            float noise = Mathf.PerlinNoise(
                (pos.x / WorldGenerator.ChunkSize + 1000f) * worldGen.biomeNoiseScale,
                (pos.y / WorldGenerator.ChunkSize + 1000f) * worldGen.biomeNoiseScale);
            int biomeIdx = Mathf.Clamp(Mathf.FloorToInt(noise * worldGen.biomes.Count), 0, worldGen.biomes.Count - 1);
            BiomeData b   = worldGen.biomes[biomeIdx];
            // Guard against 0: Inspector-created BiomeData may serialize 0 for unset fields
            biomeHPMult    = b.enemyDamageMultiplier  > 0f ? b.enemyDamageMultiplier  : 1f;
            biomeDmgMult   = b.enemyDamageMultiplier  > 0f ? b.enemyDamageMultiplier  : 1f;
            biomeSpeedMult = b.enemySpeedMultiplier   > 0f ? b.enemySpeedMultiplier   : 1f;
        }
        GameObject go = new GameObject($"Enemy_{cfg.enemyTypeName}");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 10f;
        go.tag = "Enemy";  // required by WeaponSystem projectile collision

        // SpriteRenderer (needed by EnemyAnimator)
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5; // above POI (1), gems (4), below player (6)

        // Rigidbody2D — kinematic so the code drives position directly
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // CircleCollider2D — non-trigger so the projectile's trigger collider detects it
        // (two trigger/kinematic combos don't reliably fire OnTriggerEnter2D)
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = false;
        col.radius    = 0.08f; // world radius = 0.08 × scale(10) = 0.8u

        // Prevent physical de-penetration forces from pushing the player.
        // The enemy moves via transform.position directly (bypasses physics),
        // but Unity still resolves overlapping non-trigger vs Dynamic colliders.
        var playerTransform = SurvivorMasterScript.Instance?.player;
        if (playerTransform != null) {
            foreach (var pc in playerTransform.GetComponents<Collider2D>())
                Physics2D.IgnoreCollision(col, pc);
        }

        // EnemyEntity
        EnemyEntity entity  = go.AddComponent<EnemyEntity>();
        entity.behavior     = cfg.behavior;
        entity.hp           = hp * biomeHPMult;
        entity.moveSpeed    = cfg.moveSpeed * biomeSpeedMult;
        entity.attackRange  = cfg.attackRange;

        // EnemyAttack
        EnemyAttack attack      = go.AddComponent<EnemyAttack>();
        attack.type             = cfg.behavior == EnemyBehavior.Ranged ? AttackType.Projectile : AttackType.Melee;
        attack.damage           = damage * biomeDmgMult;
        attack.attackInterval   = cfg.attackInterval;

        // EnemyAnimator — load sprites if a folder exists
        EnemyAnimator anim = go.AddComponent<EnemyAnimator>();
        if (!string.IsNullOrEmpty(cfg.enemyTypeName))
            anim.LoadClipsForEnemy(cfg.enemyTypeName);
    }

    // ── Position selection ─────────────────────────────────────────────────────
    Vector3 PickSpawnPosition() {
        if (worldGen == null || SurvivorMasterScript.Instance == null)
            return Vector3.zero;

        Vector3 playerPos = SurvivorMasterScript.Instance.player.position;

        // Collect chunks that are far enough from the player
        var candidates = new List<Vector2Int>();
        foreach (var key in worldGen.ActiveChunkKeys) {
            Vector3 center = worldGen.GetChunkWorldCenter(key);
            if (Vector3.Distance(center, playerPos) >= minSpawnDistFromPlayer)
                candidates.Add(key);
        }

        // Fall back: any available chunk
        if (candidates.Count == 0) {
            foreach (var key in worldGen.ActiveChunkKeys)
                candidates.Add(key);
        }

        if (candidates.Count == 0) return playerPos + Vector3.right * 15f;

        Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
        // Random offset within the chunk (avoid edges)
        float halfChunk = WorldGenerator.ChunkSize * 0.5f;
        float ox = Random.Range(-halfChunk * 0.8f, halfChunk * 0.8f);
        float oy = Random.Range(-halfChunk * 0.8f, halfChunk * 0.8f);
        return worldGen.GetChunkWorldCenter(chosen) + new Vector3(ox, oy, 0f);
    }
}
