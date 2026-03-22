using System.Collections.Generic;
using UnityEngine;

public class ZenithDatabaseLoader : MonoBehaviour {
    public bool overwriteExisting = false;

    void Awake() {
        LoadDatabase();
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
        // Adding a few examples based on your "Survivor_OmniEngine_Zenith" logic
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

    void AddWeapon(WeaponSystem ws, string name, string desc, Rarity rare, int weight, float dmg, float cd, int pierce, WeaponTrait trait, List<string> tags, bool isWeapon = true) {
        if (ws.cardPool.Exists(x => x.itemName == name)) return;
        
        ws.cardPool.Add(new ItemData {
            itemName = name,
            description = desc,
            rarity = rare,
            weight = weight,
            baseDamage = dmg,
            cooldown = cd,
            pierceCount = pierce,
            trait = trait,
            tags = tags,
            isWeapon = isWeapon
        });
    }

    void AddRecipe(WeaponSystem ws, string wA, string iB, string result) {
        if (ws.recipes.Exists(x => x.resultName == result)) return;
        ws.recipes.Add(new EvolutionRecipe { weaponA = wA, itemB = iB, resultName = result });
    }
}