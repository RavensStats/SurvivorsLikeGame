using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Linq;

public class SurvivorMasterScript : MonoBehaviour {
        public void ResetUltimate() {
            ultTimer = 0;
            ultFill.value = 0;
        }
        public void FillUltimate() {
            ultTimer = ultCooldown;
            if (ultFill != null) ultFill.value = 1f;
        }
        public void ChargeUlt(float fraction) {
            ultTimer = Mathf.Min(ultTimer + ultCooldown * fraction, ultCooldown);
            if (ultFill != null) ultFill.value = ultTimer / ultCooldown;
        }
    public static SurvivorMasterScript Instance;
    public SpatialGrid Grid;

    [Header("Player & Meta")]
    public GameObject playerPrefab;
    public Transform player;
    public CharacterClass currentClass;
    public float playerHP = 100, xp, xpMax = 100;
    public int playerLevel = 1;
    public List<BestiaryEntry> bestiary = new List<BestiaryEntry>();
    // O(1) lookup companion to the bestiary list – populated alongside it in RegisterKill().
    public Dictionary<EnemyBehavior, BestiaryEntry> BestiaryLookup = new Dictionary<EnemyBehavior, BestiaryEntry>();
    public NemesisData nemesis;
    public static int GlobalGold;
    public bool isInsideGraveyard, isInvulnerable;

    // Kill counts broken out by enemy tier — used by Necromancer ult weighted random.
    public readonly Dictionary<EnemyBehavior, int> EliteTierKillCounts = new Dictionary<EnemyBehavior, int>();
    public readonly Dictionary<EnemyBehavior, int> BossTierKillCounts  = new Dictionary<EnemyBehavior, int>();

    // ── POI modifier state (reset on run start; set/cleared by POIInstance) ──
    [HideInInspector] public float poiDamageMult    = 1f;  // Forge
    [HideInInspector] public float poiCooldownMult  = 1f;  // Mana Well
    [HideInInspector] public float poiGoldMult      = 1f;  // Golden Statue / Thieves Den
    [HideInInspector] public int   poiExtraCards    = 0;   // Cursed Altar
    [HideInInspector] public bool  poiBlockKnockback = false; // Monolith
    [HideInInspector] public bool  poiHalfSpeed     = false; // Toxic Pit

    [Header("UI References")]
    public Slider hpSlider, xpSlider, ultFill;
    public Text timerText, goldText;

    private float gameTime, ultTimer, ultCooldown = 60f;
    private float maxPlayerHP, _baseMaxPlayerHP;
    private float regenRate, regenInterval = 5f, regenTimer;
    private float _statSnapshotTimer;
    private const float StatSnapshotInterval = 5f;
    public float GameTime => gameTime;
    public float UltTimer => ultTimer;
    public void DrainUlt(float amt) => ultTimer = Mathf.Max(0f, ultTimer - amt);
    public float UltCooldown => ultCooldown;
    public float HPRatio => maxPlayerHP > 0f ? Mathf.Clamp01(playerHP / maxPlayerHP) : 1f;

    // Cached main camera – avoids repeated tag-based GameObject.FindWithTag("MainCamera")
    // calls that Camera.main performs internally on every access.
    private static Camera _mainCam;

    // Returns true when worldPos falls within the camera's current viewport.
    // Used to gate enemy movement and weapon targeting to on-screen entities only.
    public static bool IsOnScreen(Vector3 worldPos) {
        if (_mainCam == null) { _mainCam = Camera.main; if (_mainCam == null) return true; }
        Vector3 vp = _mainCam.WorldToViewportPoint(worldPos);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }

    // ── Run stats (reset each run) ─────────────────────────────────────────
    [HideInInspector] public float totalDamageDealt;
    [HideInInspector] public float totalDamageReceived;
    [HideInInspector] public float totalXPGained;
    [HideInInspector] public int   totalGoldGained;
    [HideInInspector] public int   totalEnemiesKilled;

    void Awake() {
        Instance = this;
        _mainCam = Camera.main;
        Grid = new SpatialGrid(12f);
        XpGem.Init();
        if (OutlineManager.Instance == null)
            new GameObject("OutlineManager").AddComponent<OutlineManager>();
        if (LevelUpManager.Instance == null)
            new GameObject("LevelUpManager").AddComponent<LevelUpManager>();
        if (playerPrefab != null) {
            GameObject p = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player = p.transform;
            // Auto-assign to CameraFollow if present
            CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
            if (cam != null) cam.target = player;
        } else {
            Debug.LogWarning($"[SurvivorMasterScript] Player NOT instantiated. playerPrefab={(playerPrefab == null ? "NULL" : playerPrefab.name)}, player={(player == null ? "NULL" : player.name)}");
        }
        GlobalGold = PlayerPrefs.GetInt("TotalGold", 0);
        FloatingText.RefreshSettings();
        EnemyEntity.RefreshHPBarSettings();
        playerHP += PersistentUpgrades.MaxHPBonus;
        maxPlayerHP = playerHP;
        _baseMaxPlayerHP = maxPlayerHP;
        string nemJson = PlayerPrefs.GetString("Nemesis", "");
        nemesis = string.IsNullOrEmpty(nemJson) ? new NemesisData() : JsonUtility.FromJson<NemesisData>(nemJson);
    }

    void Update() {
        if (Time.timeScale <= 0) return;
        gameTime += Time.deltaTime;

        // Biome time tracking
        if (WorldGenerator.Instance != null)
            RunStatistics.AddBiomeTime(WorldGenerator.Instance.CurrentBiomeName, Time.deltaTime);

        // Damage snapshot for the graph (every 5 seconds)
        _statSnapshotTimer += Time.deltaTime;
        if (_statSnapshotTimer >= StatSnapshotInterval) {
            RunStatistics.TakeSnapshot(gameTime);
            _statSnapshotTimer = 0f;
        }

        if (ultTimer < ultCooldown) {
            ultTimer += Time.deltaTime;
            if (ultFill != null) ultFill.value = ultTimer / ultCooldown;
        }
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && ultTimer >= ultCooldown) ExecuteUltimate();
        if (regenRate > 0f) {
            regenTimer += Time.deltaTime;
            if (regenTimer >= regenInterval) {
                Heal(regenRate);
                regenTimer = 0f;
            }
        }
        UpdateUI();
    }

    void ExecuteUltimate() {
        ultTimer = 0;
        RunStatistics.UltimateUses++;
        var targets = Grid.GetNearby(player.position);
        switch (currentClass) {
            case CharacterClass.Mage: foreach(var e in targets) e.transform.position = player.position; break;
            case CharacterClass.Samurai: targets.ForEach(e => e.TakeDamage(999)); break;
            case CharacterClass.Cryomancer: StartCoroutine(TimeStop(5f)); break;
            case CharacterClass.Vampire: Heal(targets.Count * 2f); break;
            case CharacterClass.Sniper: WeaponSystem.Instance.RapidFire(3f); break;
            case CharacterClass.Cleric: {
                Heal(maxPlayerHP);
                // Convert on-screen enemies: 50% of normals, 50% per elite, 10% per boss.
                var all = Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
                var normals = new List<EnemyEntity>();
                foreach (var e in all) {
                    if (e.isDead || e.isCharmed || e.isPermanentlyCharmed) continue;
                    if (!IsOnScreen(e.transform.position)) continue;
                    if      (e.tier == EnemyTier.Elite && Random.value < 0.5f) e.isPermanentlyCharmed = true;
                    else if (e.tier == EnemyTier.Boss  && Random.value < 0.1f) e.isPermanentlyCharmed = true;
                    else if (e.tier == EnemyTier.Normal) normals.Add(e);
                }
                // Shuffle normals then convert the first half.
                for (int i = normals.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = normals[i]; normals[i] = normals[j]; normals[j] = t; }
                int n = Mathf.CeilToInt(normals.Count * 0.5f);
                for (int i = 0; i < n; i++) normals[i].isPermanentlyCharmed = true;
                break;
            }
            case CharacterClass.Bard: targets.ForEach(e => e.Stun(4f)); break;
            case CharacterClass.Merchant: GlobalGold += 100; break;
            case CharacterClass.Necromancer: {
                // 75%: spawn elite of any killed enemy type; 25%: spawn boss of a killed boss type.
                bool spawnBoss = Random.value < 0.25f;
                bool isB = false;
                EnemyBehavior bt;
                if (spawnBoss && BossTierKillCounts.Count > 0) {
                    bt = WeaponSystem.WeightedRandom(BossTierKillCounts);
                    isB = true;
                } else if (BestiaryLookup.Count > 0) {
                    bt = WeaponSystem.WeightedRandom(BestiaryLookup, totalEnemiesKilled);
                } else {
                    bt = EnemyBehavior.Chaser;
                }
                Vector3 sp = player.position + (Vector3)(Random.insideUnitCircle.normalized * 4f);
                WeaponSystem.Instance?.SpawnNecroMinion(bt, sp, 30f, 1f, isElite: !isB, isBoss: isB);
                break;
            }
            case CharacterClass.PuppetMaster: WeaponSystem.Instance?.SpawnUltimateClones(); break;
            case CharacterClass.Chronomancer:
                FloatingText.SpawnBanner("Chronometric Blitz", new Color(0.9f, 0.5f, 1f, 1f));
                WeaponSystem.Instance?.ActivateAllTemporalEffects();
                break;
            // Additional classes use target.TakeDamage(100) as default
            default: targets.ForEach(e => e.TakeDamage(100)); break;
        }
    }

    IEnumerator TimeStop(float d) { Time.timeScale = 0.2f; yield return new WaitForSecondsRealtime(d); Time.timeScale = 1; }

    public void GainXP(float amt) {
        float actual = amt * PersistentUpgrades.XPRateMult * (1f + RunUpgrades.XPRateBonus) * WeaponSystem.TemporalXPMult;
        xp += actual;
        totalXPGained += actual;
        while (xp >= xpMax) {
            xp -= xpMax;   // carry overflow into next level
            xpMax *= 1.2f;
            playerLevel++;
            LevelUpManager.Instance?.Show(3 + poiExtraCards);
        }
    }

    public void RegisterDamageDealt(float amt) => totalDamageDealt += amt;

    public void ResetRunStats() {
        totalDamageDealt = 0; totalDamageReceived = 0;
        totalXPGained = 0; totalGoldGained = 0; totalEnemiesKilled = 0;
        EliteTierKillCounts.Clear();
        BossTierKillCounts.Clear();
        RunStatistics.Reset();
        _statSnapshotTimer = 0f;
        playerLevel = 1; xp = 0; xpMax = 100;
        maxPlayerHP = _baseMaxPlayerHP;
        playerHP = maxPlayerHP;
        gameTime = 0f;
        regenRate = 0f;
        regenTimer = 0f;
        // Reset per-run movement speed boost.
        player?.GetComponent<PlayerMovement>()?.ResetForNewRun();
        poiDamageMult     = 1f;
        poiCooldownMult   = 1f;
        poiGoldMult       = 1f;
        poiExtraCards     = 0;
        poiBlockKnockback = false;
        poiHalfSpeed      = false;
        // Must be explicit — POIInstance.OnDestroy only resets this when the player was
        // inside at destruction time, so dying outside the zone leaks the flag into the
        // next run and makes every enemy permanently 1.57× faster.
        isInsideGraveyard = false;
        isInvulnerable    = false;
    }

    public void BonusMaxHP(float amount) {
        maxPlayerHP += amount;
        playerHP    += amount;
    }

    // Restores HP up to the current maximum.
    public void Heal(float amount) {
        if (amount <= 0f) return;
        float prev = playerHP;
        playerHP = Mathf.Min(playerHP + amount, maxPlayerHP);
        float healed = playerHP - prev;
        if (healed > 0f && player != null)
            FloatingText.SpawnHeal(player.position, healed);
    }

    public void EnableRegen(float hpPerTick, float interval) {
        regenRate     += hpPerTick;
        regenInterval  = Mathf.Max(0.1f, interval);
    }

    public void RegisterKill(EnemyBehavior type) {
        totalEnemiesKilled++;
        RunStatistics.RecordKill(type);
        if (!BestiaryLookup.TryGetValue(type, out var b)) {
            b = new BestiaryEntry { behavior = type };
            BestiaryLookup[type] = b;
            bestiary.Add(b);
        }
        b.kills++;
        if (b.kills >= 500) b.isHunterBonusUnlocked = true;

        if (WeaponSystem.SlaughterTimeActive) ChargeUlt(0.01f);

        // Vampire: restore 3 HP every 5th kill
        if (currentClass == CharacterClass.Vampire && totalEnemiesKilled % 5 == 0)
            Heal(3f);
    }

    public void RegisterKill(EnemyBehavior type, EnemyTier tier) {
        RegisterKill(type); // delegate to main method
        if (tier == EnemyTier.Elite) {
            if (!EliteTierKillCounts.ContainsKey(type)) EliteTierKillCounts[type] = 0;
            EliteTierKillCounts[type]++;
        } else if (tier == EnemyTier.Boss) {
            if (!BossTierKillCounts.ContainsKey(type)) BossTierKillCounts[type] = 0;
            BossTierKillCounts[type]++;
        }
    }

    public void TakeDamage(float amt) {
        if (isInvulnerable) return;
        amt = Mathf.Round(amt);
        playerHP -= amt;
        totalDamageReceived += amt;
        if (amt > 0f && player != null)
            FloatingText.SpawnPlayerDamage(player.position, amt);
        if (playerHP <= 0) {
            playerHP = 0;
            Time.timeScale = 0f;
            var record = new RunRecord {
                characterName  = PlayerPrefs.GetString("SelectedCharacter", "Unknown"),
                level          = playerLevel,
                timePlayed     = (int)gameTime,
                damageDealt    = (int)totalDamageDealt,
                damageReceived = (int)totalDamageReceived,
                xpGained       = (int)totalXPGained,
                goldGained     = totalGoldGained,
                enemiesKilled  = totalEnemiesKilled
            };
            MainMenuManager mm = Object.FindFirstObjectByType<MainMenuManager>();
            if (mm != null) mm.ShowGameOver(record);
        }
    }

    void UpdateUI() {
        if (timerText != null) timerText.text = $"{(int)gameTime/60:00}:{(int)gameTime%60:00}";
        if (goldText != null) goldText.text = $"Gold: {GlobalGold}";
        if (hpSlider != null) hpSlider.value = maxPlayerHP > 0 ? playerHP / maxPlayerHP : 0f;
        if (xpSlider != null) xpSlider.value = xp / xpMax;
    }
}