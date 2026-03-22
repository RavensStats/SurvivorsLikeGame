using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class SurvivorMasterScript : MonoBehaviour {
        public void ResetUltimate() {
            ultTimer = 0;
            ultFill.value = 0;
        }
    public static SurvivorMasterScript Instance;
    public SpatialGrid Grid;

    [Header("Player & Meta")]
    public Transform player;
    public CharacterClass currentClass;
    public float playerHP = 100, xp, xpMax = 100;
    public List<BestiaryEntry> bestiary = new List<BestiaryEntry>();
    public NemesisData nemesis;
    public static int GlobalGold;
    public bool isInsideGraveyard, isInvulnerable;

    [Header("UI References")]
    public GameObject levelUpPanel, xpGemPrefab;
    public Slider hpSlider, xpSlider, ultFill;
    public Text timerText, goldText;

    private float gameTime, ultTimer, ultCooldown = 60f;

    void Awake() {
        Instance = this;
        Grid = new SpatialGrid(12f);
        GlobalGold = PlayerPrefs.GetInt("TotalGold", 0);
        string nemJson = PlayerPrefs.GetString("Nemesis", "");
        nemesis = string.IsNullOrEmpty(nemJson) ? new NemesisData() : JsonUtility.FromJson<NemesisData>(nemJson);
    }

    void Update() {
        if (Time.timeScale <= 0) return;
        gameTime += Time.deltaTime;
        if (ultTimer < ultCooldown) {
            ultTimer += Time.deltaTime;
            ultFill.value = ultTimer / ultCooldown;
        }
        if (Input.GetKeyDown(KeyCode.Space) && ultTimer >= ultCooldown) ExecuteUltimate();
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
        xp += amt;
        if (xp >= xpMax) { Time.timeScale = 0; levelUpPanel.SetActive(true); xp = 0; xpMax *= 1.2f; }
    }

    public void RegisterKill(EnemyBehavior type) {
        var b = bestiary.Find(x => x.behavior == type) ?? new BestiaryEntry { behavior = type };
        if (!bestiary.Contains(b)) bestiary.Add(b);
        b.kills++;
        if (b.kills >= 500) b.isHunterBonusUnlocked = true;
    }

    void UpdateUI() {
        timerText.text = $"{(int)gameTime/60:00}:{(int)gameTime%60:00}";
        goldText.text = $"Gold: {GlobalGold}";
        hpSlider.value = playerHP / 100f;
        xpSlider.value = xp / xpMax;
    }
}