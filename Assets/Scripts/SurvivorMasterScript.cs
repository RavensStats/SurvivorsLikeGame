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
    public static SurvivorMasterScript Instance;
    public SpatialGrid Grid;

    [Header("Player & Meta")]
    public GameObject playerPrefab;
    public Transform player;
    public CharacterClass currentClass;
    public float playerHP = 100, xp, xpMax = 100;
    public int playerLevel = 1;
    public List<BestiaryEntry> bestiary = new List<BestiaryEntry>();
    public NemesisData nemesis;
    public static int GlobalGold;
    public bool isInsideGraveyard, isInvulnerable;

    [Header("UI References")]
    public Slider hpSlider, xpSlider, ultFill;
    public Text timerText, goldText;

    private float gameTime, ultTimer, ultCooldown = 60f;
    private float maxPlayerHP;
    private float regenRate, regenInterval = 5f, regenTimer;
    public float GameTime => gameTime;
    public float UltTimer => ultTimer;
    public float UltCooldown => ultCooldown;

    // ── Run stats (reset each run) ─────────────────────────────────────────
    [HideInInspector] public float totalDamageDealt;
    [HideInInspector] public float totalDamageReceived;
    [HideInInspector] public float totalXPGained;
    [HideInInspector] public int   totalGoldGained;
    [HideInInspector] public int   totalEnemiesKilled;

    void Awake() {
        Instance = this;
        Grid = new SpatialGrid(12f);
        XpGem.Init();
        if (LevelUpManager.Instance == null)
            new GameObject("LevelUpManager").AddComponent<LevelUpManager>();
        if (playerPrefab != null) {
            GameObject p = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player = p.transform;
            Debug.Log("[SurvivorMasterScript] Player instantiated at origin.");
            // Auto-assign to CameraFollow if present
            CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
            if (cam != null) cam.target = player;
        } else {
            Debug.LogWarning($"[SurvivorMasterScript] Player NOT instantiated. playerPrefab={(playerPrefab == null ? "NULL" : playerPrefab.name)}, player={(player == null ? "NULL" : player.name)}");
        }
        GlobalGold = PlayerPrefs.GetInt("TotalGold", 0);
        playerHP += PersistentUpgrades.MaxHPBonus;
        maxPlayerHP = playerHP;
        string nemJson = PlayerPrefs.GetString("Nemesis", "");
        nemesis = string.IsNullOrEmpty(nemJson) ? new NemesisData() : JsonUtility.FromJson<NemesisData>(nemJson);
    }

    void Update() {
        if (Time.timeScale <= 0) return;
        gameTime += Time.deltaTime;
        if (ultTimer < ultCooldown) {
            ultTimer += Time.deltaTime;
            if (ultFill != null) ultFill.value = ultTimer / ultCooldown;
        }
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && ultTimer >= ultCooldown) ExecuteUltimate();
        if (regenRate > 0f) {
            regenTimer += Time.deltaTime;
            if (regenTimer >= regenInterval) { playerHP = Mathf.Min(playerHP + regenRate, maxPlayerHP); regenTimer = 0f; }
        }
        UpdateUI();
    }

    void ExecuteUltimate() {
        ultTimer = 0;
        var targets = Grid.GetNearby(player.position);
        switch (currentClass) {
            case CharacterClass.Mage: foreach(var e in targets) e.transform.position = player.position; break;
            case CharacterClass.Samurai: targets.ForEach(e => e.TakeDamage(999)); break;
            case CharacterClass.Cryomancer: StartCoroutine(TimeStop(5f)); break;
            case CharacterClass.Vampire: playerHP += targets.Count * 2; break;
            case CharacterClass.Sniper: WeaponSystem.Instance.RapidFire(3f); break;
            case CharacterClass.Cleric: playerHP = 100; break;
            case CharacterClass.Bard: targets.ForEach(e => e.Stun(4f)); break;
            case CharacterClass.Merchant: GlobalGold += 100; break;
            // Additional classes use target.TakeDamage(100) as default
            default: targets.ForEach(e => e.TakeDamage(100)); break;
        }
    }

    IEnumerator TimeStop(float d) { Time.timeScale = 0.2f; yield return new WaitForSecondsRealtime(d); Time.timeScale = 1; }

    public void GainXP(float amt) {
        float actual = amt * PersistentUpgrades.XPRateMult * (1f + RunUpgrades.XPRateBonus);
        xp += actual;
        totalXPGained += actual;
        if (xp >= xpMax) { xp = 0; xpMax *= 1.2f; playerLevel++; LevelUpManager.Instance?.Show(3); }
    }

    public void RegisterDamageDealt(float amt) => totalDamageDealt += amt;

    public void ResetRunStats() {
        totalDamageDealt = 0; totalDamageReceived = 0;
        totalXPGained = 0; totalGoldGained = 0; totalEnemiesKilled = 0;
        playerLevel = 1; xp = 0; xpMax = 100;
        playerHP = maxPlayerHP;
        gameTime = 0f;
        regenRate = 0f;
        regenTimer = 0f;
    }

    public void BonusMaxHP(float amount) {
        maxPlayerHP += amount;
        playerHP    += amount;
    }

    public void EnableRegen(float hpPerTick, float interval) {
        regenRate     += hpPerTick;
        regenInterval  = Mathf.Max(0.1f, interval);
    }

    public void RegisterKill(EnemyBehavior type) {
        totalEnemiesKilled++;
        var b = bestiary.Find(x => x.behavior == type) ?? new BestiaryEntry { behavior = type };
        if (!bestiary.Contains(b)) bestiary.Add(b);
        b.kills++;
        if (b.kills >= 500) b.isHunterBonusUnlocked = true;
    }

    public void TakeDamage(float amt) {
        if (isInvulnerable) return;
        playerHP -= amt;
        totalDamageReceived += amt;
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