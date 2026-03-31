using System.Collections.Generic;
using UnityEngine;

public class ZenithDatabaseLoader : MonoBehaviour {
    public static ZenithDatabaseLoader Instance;
    public bool overwriteExisting = false;

    [Header("Starting Weapon Prefabs")]
    public GameObject wandProjectilePrefab;
    public GameObject bowProjectilePrefab;

    void Awake() {
        Instance = this;
        LoadDatabase();
    }

    public void AssignStartingWeapon() {
        WeaponSystem ws = GetComponent<WeaponSystem>();
        if (ws == null || SurvivorMasterScript.Instance == null) return;

        CharacterClass cls = SurvivorMasterScript.Instance.currentClass;
        string name = cls == CharacterClass.Mage   ? "Wand"
                    : cls == CharacterClass.Ranger  ? "Hunter's Bow"
                    : null;
        if (name == null) return;

        ItemData weapon = ws.cardPool.Find(w => w.itemName == name);
        if (weapon == null) { Debug.LogWarning($"[ZenithDatabaseLoader] Starting weapon '{name}' not found in pool."); return; }

        if (name == "Wand" && wandProjectilePrefab != null)            weapon.projectilePrefab = wandProjectilePrefab;
        else if (name == "Hunter's Bow" && bowProjectilePrefab != null) weapon.projectilePrefab = bowProjectilePrefab;

        ws.activeWeapons.Add(weapon);
        Debug.Log($"[ZenithDatabaseLoader] Assigned '{name}' (Lv.{weapon.level}) as starting weapon for {cls}.");
    }

    [ContextMenu("Force Load Database")] // Allows you to right-click the component in Inspector to run
    public void LoadDatabase() {
        WeaponSystem ws = GetComponent<WeaponSystem>();
        if (ws == null) return;

        if (overwriteExisting) {
            ws.cardPool.Clear();
            ws.recipes.Clear();
        }

        // --- 1. POPULATE WEAPONS & ITEMS ---
        AddWeapon(ws, "Wand", "Fires a bolt at the nearest N enemies (N = Wand level).", Rarity.Common, 10, 20f, 1.2f, 1, WeaponTrait.None, new List<string>{"Magic", "Projectiles"}, fireMode: FireMode.NearestN, knockback: 1f);
        AddWeapon(ws, "Hunter's Bow", "Fires an arrow at up to N random enemies in range (N = Bow level).", Rarity.Common, 10, 8f, 2.0f, 1, WeaponTrait.None, new List<string>{"Physical", "Ranged"}, fireMode: FireMode.RandomInRange, range: 20f);
        AddWeapon(ws, "Magic Wand", "Fires magic bolts at the nearest enemy.", Rarity.Common, 10, 5f, 1.2f, 1, WeaponTrait.None, new List<string>{"Magic", "Projectiles"});
        AddWeapon(ws, "Axe", "Swings in a high-damage arc.", Rarity.Common, 8, 15f, 2.5f, 3, WeaponTrait.Piercing, new List<string>{"Physical", "Melee"});
        AddWeapon(ws, "Fire Orb", "Orbits the player, burning enemies.", Rarity.Rare, 5, 8f, 3.0f, 99, WeaponTrait.Rotating, new List<string>{"Fire", "Magic"});
        AddWeapon(ws, "Empty Tome", "Increases cooldown speed of all weapons.", Rarity.Common, 10, 0, 0, 0, WeaponTrait.None, new List<string>{"Utility", "Magic"}, false);
        AddWeapon(ws, "Heavy Bracers", "Increases knockback power.", Rarity.Common, 10, 0, 0, 0, WeaponTrait.None, new List<string>{"Heavy", "Physical"}, false);

        // --- 2. POPULATE EVOLUTION RECIPES ---
        AddRecipe(ws, "Magic Wand", "Empty Tome", "Holy Scepter");
        AddRecipe(ws, "Axe", "Heavy Bracers", "Death Spiral");
        AddRecipe(ws, "Fire Orb", "Mana Well", "Supernova");

        Debug.Log("Zenith Database Initialized: " + ws.cardPool.Count + " items loaded.");
    }

    void AddWeapon(WeaponSystem ws, string name, string desc, Rarity rare, int weight, float dmg, float cd, int pierce, WeaponTrait trait, List<string> tags, bool isWeapon = true, FireMode fireMode = FireMode.Default, float range = 0f, int level = 1, float knockback = 0f) {
        if (ws.cardPool.Exists(x => x.itemName == name)) return;
        
        ws.cardPool.Add(new ItemData {
            itemName = name,
            description = desc,
            rarity = rare,
            baseDamage = dmg,
            cooldown = cd,
            pierceCount = pierce,
            trait = trait,
            tags = tags,
            isWeapon = isWeapon,
            fireMode = fireMode,
            range = range,
            level = level,
            knockback = knockback
        });
    }

    void AddRecipe(WeaponSystem ws, string wA, string iB, string result) {
        if (ws.recipes.Exists(x => x.resultName == result)) return;
        ws.recipes.Add(new EvolutionRecipe { weaponA = wA, itemB = iB, resultName = result });
    }
}