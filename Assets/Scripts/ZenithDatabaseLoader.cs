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

        // Always wire all known prefabs so any weapon picked up later fires correctly.
        var wandItem = ws.cardPool.Find(w => w.itemName == "Magician's Wand");
        if (wandItem != null && wandProjectilePrefab != null) wandItem.projectilePrefab = wandProjectilePrefab;
        var bowItem = ws.cardPool.Find(w => w.itemName == "Hunter's Bow");
        if (bowItem != null && bowProjectilePrefab != null) bowItem.projectilePrefab = bowProjectilePrefab;

        CharacterClass cls = SurvivorMasterScript.Instance.currentClass;
        string name = cls == CharacterClass.Mage             ? "Magician's Wand"
                    : cls == CharacterClass.Ranger           ? "Hunter's Bow"
                    : cls == CharacterClass.Knight           ? "Longsword"
                    : cls == CharacterClass.Pyromancer       ? "Pyromancy"
                    : cls == CharacterClass.Rogue            ? "Blowgun"
                    : cls == CharacterClass.Necromancer      ? "Summon Undead"
                    : cls == CharacterClass.Paladin          ? "Sword of the Heavens"
                    : cls == CharacterClass.Engineer         ? "Sentry Gun"
                    : cls == CharacterClass.Alchemist        ? "Acid Flask"
                    : cls == CharacterClass.Merchant         ? "Gold Coin"
                    : cls == CharacterClass.Berserker        ? "Axe"
                    : cls == CharacterClass.Ghost            ? "Spectral Beam"
                    : cls == CharacterClass.Vampire          ? "Blood Pool"
                    : cls == CharacterClass.Samurai          ? "Katana"
                    : cls == CharacterClass.Bard             ? "Ballad"
                    : cls == CharacterClass.Sniper           ? "Sniper Rifle"
                    : cls == CharacterClass.Monk             ? "Fist of Absolution"
                    : cls == CharacterClass.Druid            ? "Creeping Vines"
                    : cls == CharacterClass.Cyborg           ? "Radial Laser"
                    : cls == CharacterClass.Cleric           ? "Holy Staff"
                    : cls == CharacterClass.Cryomancer       ? "Ice Shard"
                    : cls == CharacterClass.Warlock          ? "Chain Lightning"
                    : cls == CharacterClass.Assassin         ? "Assassin Blade"
                    : cls == CharacterClass.Gladiator        ? "Trident"
                    : cls == CharacterClass.Tactician        ? "Caltrop Throw"
                    : cls == CharacterClass.Chronomancer     ? "Time Manipulation"
                    : cls == CharacterClass.VoidCaller       ? "Void Orb"
                    : cls == CharacterClass.Beastmaster      ? "Wolf Claws"
                    : cls == CharacterClass.NecroKnight      ? "Scythe"
                    : cls == CharacterClass.ArcaneArcher     ? "Arcane Arrow"
                    : cls == CharacterClass.PlagueDoctor     ? "Plague Canister"
                    : cls == CharacterClass.Gunslinger       ? "Dual Revolvers"
                    : cls == CharacterClass.Viking           ? "Throwing Axe"
                    : cls == CharacterClass.Junker           ? "Scrap Maul"
                    : cls == CharacterClass.Oracle           ? "Oracle Beam"
                    : cls == CharacterClass.PuppetMaster     ? "Shadow Clone"
                    : cls == CharacterClass.Enchanter        ? "Binding Circle"
                    : cls == CharacterClass.Artificer        ? "Saw Blade"
                    : cls == CharacterClass.Ninja            ? "Shuriken"
                    : cls == CharacterClass.ArcticScout      ? "Frost Bolt"
                    : cls == CharacterClass.Dwarf            ? "War Hammer"
                    : cls == CharacterClass.Hydromancer      ? "Downpour"
                    : cls == CharacterClass.Hydrokineticist  ? "Tidal Wave"
                    : cls == CharacterClass.Battlemage       ? "Spirit Aura"
                    : cls == CharacterClass.Psychomancer     ? "Psychic Blast"
                    : null;
        if (name == null) return;

        ItemData weapon = ws.cardPool.Find(w => w.itemName == name);
        if (weapon == null) { Debug.LogWarning($"[ZenithDatabaseLoader] Starting weapon '{name}' not found in pool."); return; }

        ws.activeWeapons.Add(weapon);
    }

    /// <summary>Restores the card pool to its initial state (re-adds any items removed by evolution).</summary>
    public void ReloadDatabase() {
        bool prev = overwriteExisting;
        overwriteExisting = true;
        LoadDatabase();
        overwriteExisting = prev;
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
        AddWeapon(ws, "Magician's Wand", "Fires a bolt at the nearest N enemies (N = level).",               Rarity.Common, 20f, 1.2f, 1,  WeaponTrait.None,     new List<string>{"Magic", "Projectiles"},  fireMode: FireMode.NearestN,      knockback: 1f, scale: 10f);
        AddWeapon(ws, "Hunter's Bow",    "Fires an arrow at up to N random enemies in range (N = level).",   Rarity.Common, 8f,  1.8f, 1,  WeaponTrait.None,     new List<string>{"Physical", "Ranged"},    fireMode: FireMode.RandomInRange,  range: 35f,  spriteFolder: "Arrow", scale: 10f);
        AddWeapon(ws, "Longsword",        "Slashes nearby enemies in a quick arc.",                           Rarity.Common, 12f, 0.8f, 1,  WeaponTrait.None,     new List<string>{"Physical", "Melee"},    fireMode: FireMode.ArcSwing,       range: 12f, spriteFolder: "Sword");
        AddWeapon(ws, "Axe",              "Swings in a high-damage arc.",                                     Rarity.Common, 15f, 2.5f, 3,  WeaponTrait.Piercing, new List<string>{"Physical", "Melee"},    fireMode: FireMode.ArcSwing,       range: 4f, spriteFolder: "Axe");
        AddWeapon(ws, "Pyromancy",        "Orbits the player, burning enemies.",                              Rarity.Rare,   8f,  3.0f, 99, WeaponTrait.Rotating, new List<string>{"Fire", "Magic"},       fireMode: FireMode.Orbit, spriteFolder: "Flame");
        AddWeapon(ws, "Empty Tome",   "Increases cooldown speed of all weapons.",                          Rarity.Common, 0,   0,    0,  WeaponTrait.None,     new List<string>{"Utility", "Magic"},    false);
        AddWeapon(ws, "Heavy Bracers","Increases knockback power.",                                        Rarity.Common, 0,   0,    0,  WeaponTrait.None,     new List<string>{"Heavy", "Physical"},   false);

        // ── Batch 1: Rogue → Monk ────────────────────────────────────────────
        AddWeapon(ws, "Blowgun",        "Fires a fast dart at the nearest enemy.",                         Rarity.Common, 10f, 0.35f, 1, WeaponTrait.None,     new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,  range: 15f, spriteFolder: "Dart");
        AddWeapon(ws, "Summon Undead",       "Conjures a magical skull that hunts down nearby enemies.",        Rarity.Rare,   18f, 2.0f,  1, WeaponTrait.Explosive, new List<string>{"Magic","Necromancy"},       fireMode: FireMode.NearestN,  spriteFolder: "Summon");
        AddWeapon(ws, "Sword of the Heavens", "A blessed arc swing that channels divine energy through enemies.", Rarity.Common, 22f, 1.8f,  2, WeaponTrait.None,      new List<string>{"Physical","Melee","Holy"}, fireMode: FireMode.ArcSwing, range: 6f,  knockback: 6f, spriteFolder: "HolySword");
        AddWeapon(ws, "Sentry Gun",     "Deploys a rapid-burst turret targeting nearby enemies.",          Rarity.Rare,   8f,  0.3f,  1, WeaponTrait.None,     new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,  range: 20f, spriteFolder: "SentryGun");
        AddWeapon(ws, "Acid Flask",     "Thrown flask that shatters, leaving lingering acid.",             Rarity.Common, 12f, 1.5f,  1, WeaponTrait.Explosive,new List<string>{"Poison","Ranged"},     fireMode: FireMode.RandomInRange, range: 18f, spriteFolder: "PoisonPool");
        AddWeapon(ws, "Gold Coin",      "Tosses sharp coins that ricochet between up to 3 enemies.",       Rarity.Common, 9f,  0.6f,  3, WeaponTrait.Bouncy,   new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,  spriteFolder: "Misc/GoldCoin");
        AddWeapon(ws, "Spectral Beam",  "Pierces every enemy in a straight line.",                         Rarity.Rare,   15f, 1.4f,  999,WeaponTrait.Piercing,new List<string>{"Magic","Ranged"},      fireMode: FireMode.NearestN);
        AddWeapon(ws, "Blood Pool",     "Life-stealing bite — heals the player on every 5th kill.",        Rarity.Rare,   14f, 0.7f,  1, WeaponTrait.None,     new List<string>{"Physical","Melee"},    fireMode: FireMode.ArcSwing, range: 3f,  spriteFolder: "BloodPool");
        AddWeapon(ws, "Katana",         "Dash-slash in a straight path, hitting all enemies in line.",     Rarity.Rare,   20f, 1.0f,  5, WeaponTrait.Piercing, new List<string>{"Physical","Melee"},    fireMode: FireMode.ArcSwing, range: 15f, spriteFolder: "Katana");
        AddWeapon(ws, "Ballad",            "Spiraling notes orbit the player, confusing enemies on contact.", Rarity.Common, 7f,  2.5f,  99,WeaponTrait.Rotating, new List<string>{"Magic","Support"},     fireMode: FireMode.Orbit,    spriteFolder: "MusicNote");
        AddWeapon(ws, "Sniper Rifle",      "Long-range bolt targeting the highest-HP enemy, high pierce.",    Rarity.Rare,   35f, 3.0f,  3, WeaponTrait.Piercing, new List<string>{"Physical","Ranged"},   fireMode: FireMode.RandomInRange, range: 60f, spriteFolder: "SniperReticle");
        AddWeapon(ws, "Fist of Absolution","Rapid punches that briefly stun the target.",                     Rarity.Common, 8f,  2.5f,  1, WeaponTrait.None,     new List<string>{"Physical","Melee"},    fireMode: FireMode.RisingFist, range: 2.5f, spriteFolder: "Fist");

        // ── Batch 2: Druid → PuppetMaster ────────────────────────────────────
        AddWeapon(ws, "Creeping Vines",  "Summons a vine that pulls enemies toward its center.",            Rarity.Common, 11f, 1.6f,  2, WeaponTrait.None,     new List<string>{"Nature","Ranged"},     fireMode: FireMode.RandomInRange, range: 22f, spriteFolder: "Vines");
        AddWeapon(ws, "Radial Laser",   "Fires beams of energy at up to N nearby enemies simultaneously.", Rarity.Rare,   13f, 0.9f,  4, WeaponTrait.None,     new List<string>{"Energy","Ranged"},    fireMode: FireMode.NearestN,  range: 14f, spriteFolder: "RadialLaser");
        AddWeapon(ws, "Holy Staff",     "Pillar of light that damages enemies and briefly heals the player.", Rarity.Rare, 16f, 1.5f, 1, WeaponTrait.None,     new List<string>{"Holy","Magic"},        fireMode: FireMode.NearestN,  spriteFolder: "HolyStaff");
        AddWeapon(ws, "Ice Shard",      "Slow-penetrating ice shard that reduces enemy movement speed.",   Rarity.Common, 10f, 1.2f,  2, WeaponTrait.Piercing, new List<string>{"Ice","Ranged"},        fireMode: FireMode.NearestN,  spriteFolder: "IceShard");
        AddWeapon(ws, "Chain Lightning","Arcing electricity that chains between nearby enemies.",           Rarity.Rare,   20f, 2.0f,  1, WeaponTrait.Explosive,new List<string>{"Electric","Magic"},   fireMode: FireMode.ChainLightning, range: 25f, spriteFolder: "ChainLightning");
        AddWeapon(ws, "Assassin Blade", "High-damage lunge strike — deals 3× damage to enemies above 90% HP.", Rarity.Rare, 14f, 1.0f, 1, WeaponTrait.None, new List<string>{"Physical","Melee"},    fireMode: FireMode.ArcSwing, range: 5f,  spriteFolder: "PoisonDagger");
        AddWeapon(ws, "Trident",        "Net roots enemies in place, followed by a trident thrust.",       Rarity.Rare,   18f, 1.4f,  2, WeaponTrait.None,     new List<string>{"Physical","Melee"},    fireMode: FireMode.ArcSwing, range: 7f,  spriteFolder: "Trident");
        AddWeapon(ws, "Wolf Claws",     "Rapid three-bite flurry in a tight arc.",                         Rarity.Common, 9f,  0.3f,  1, WeaponTrait.None,     new List<string>{"Physical","Melee"},    fireMode: FireMode.ArcSwing, range: 3f,  spriteFolder: "WolfClaws");
        AddWeapon(ws, "Time Manipulation", "Orbiting time shards that warp reality and slow nearby enemies.", Rarity.Rare, 6f, 3.5f, 99, WeaponTrait.Rotating, new List<string>{"Magic","Time"},        fireMode: FireMode.Orbit);
        AddWeapon(ws, "Void Orb",       "Gravity-well projectile that pulls nearby enemies on impact.",    Rarity.Rare,   14f, 1.8f,  1, WeaponTrait.Explosive,new List<string>{"Dark","Magic"},        fireMode: FireMode.NearestN);
        AddWeapon(ws, "Scythe",         "Massive scythe orbits the player clockwise — spawns a skeleton on every kill.", Rarity.Rare,   22f, 5.0f,  4, WeaponTrait.Piercing, new List<string>{"Physical","Melee"},    fireMode: FireMode.ScytheOrbit, range: 14f, spriteFolder: "Scythe");
        AddWeapon(ws, "Arcane Arrow",   "Arrows leave a magic dust trail dealing damage over time.",       Rarity.Rare,   10f, 1.0f,  2, WeaponTrait.Piercing, new List<string>{"Magic","Ranged"},      fireMode: FireMode.RandomInRange, range: 40f, spriteFolder: "ArcaneArrow");
        AddWeapon(ws, "Plague Canister","Gas canister that explodes into a large lingering poison cloud.",  Rarity.Rare,   12f, 2.2f,  1, WeaponTrait.Explosive,new List<string>{"Poison","Ranged"},     fireMode: FireMode.RandomInRange, range: 20f, spriteFolder: "PoisonGas");
        AddWeapon(ws, "Dual Revolvers", "Rapidly fires at the two closest targets simultaneously.",        Rarity.Common, 12f, 0.4f,  1, WeaponTrait.None,     new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,  range: 25f);
        AddWeapon(ws, "Throwing Axe",   "Boomerang axe that returns, hitting enemies on both passes.",     Rarity.Common, 14f, 1.5f,  2, WeaponTrait.Bouncy,   new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,  spriteFolder: "WoodcutterAxe");
        AddWeapon(ws, "Scrap Maul",     "Heavy melee swing — chance to pop an extra XP gem on hit.",       Rarity.Common, 18f, 2.0f,  3, WeaponTrait.Piercing, new List<string>{"Physical","Melee"},    fireMode: FireMode.ArcSwing, range: 4f,  spriteFolder: "ScrapMaul");
        AddWeapon(ws, "Oracle Beam",    "Beams of light strike random nearby foes simultaneously.",        Rarity.Rare,   14f, 1.2f,  1, WeaponTrait.None,     new List<string>{"Holy","Magic"},        fireMode: FireMode.RandomInRange, range: 28f);
        AddWeapon(ws, "Shadow Clone",   "Creates a mirror clone that mimics all weapon attacks.",          Rarity.Rare,   10f, 2.0f,  1, WeaponTrait.None,     new List<string>{"Dark","Magic"},        fireMode: FireMode.NearestN);

        // ── Pool-only weapons ──────────────────────────────────────────────────
        AddWeapon(ws, "Spirit Aura",    "A swirling spiritual ring that orbits and burns nearby enemies.",    Rarity.Rare,   6f,  2.0f, 99, WeaponTrait.Rotating, new List<string>{"Magic","Spirit"},      fireMode: FireMode.Orbit,            spriteFolder: "Aura");
        AddWeapon(ws, "Caltrop Throw",  "Scatters spiked caltrops that slow and damage enemies on contact.", Rarity.Common, 10f, 1.5f,  2, WeaponTrait.None,     new List<string>{"Physical","Ranged"},   fireMode: FireMode.RandomInRange,     range: 18f, spriteFolder: "Caltrop");

        // ── New character starting weapons ─────────────────────────────────────
        AddWeapon(ws, "Binding Circle", "Conjures a binding circle that traps and damages nearby enemies.",   Rarity.Rare,   12f, 1.8f,  1, WeaponTrait.None,     new List<string>{"Magic","Support"},     fireMode: FireMode.NearestN,          range: 16f, spriteFolder: "BindingCircle");
        AddWeapon(ws, "Saw Blade",      "Launches a spinning saw blade that tears through enemies.",          Rarity.Common, 14f, 1.2f,  3, WeaponTrait.Piercing, new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,          spriteFolder: "SawBlade");
        AddWeapon(ws, "Shuriken",       "Throws a volley of shuriken at nearby targets.",                     Rarity.Common, 8f,  0.5f,  2, WeaponTrait.None,     new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,          range: 20f, spriteFolder: "Shuriken");
        AddWeapon(ws, "Frost Bolt",     "Fires a bolt of ice that slows enemies on impact.",                  Rarity.Common, 10f, 1.5f,  1, WeaponTrait.None,     new List<string>{"Ice","Ranged"},        fireMode: FireMode.NearestN,          range: 25f, spriteFolder: "Snowflake");
        AddWeapon(ws, "War Hammer",     "Massive hammer swing that sends a shockwave through enemies.",       Rarity.Common, 20f, 2.0f,  2, WeaponTrait.None,     new List<string>{"Physical","Melee"},    fireMode: FireMode.ArcSwing,          range: 5f, knockback: 8f, spriteFolder: "WarHammer");
        AddWeapon(ws, "Downpour",        "Fires a torrent of water bolts at nearby enemies.",                  Rarity.Common, 9f,  1.0f,  1, WeaponTrait.None,     new List<string>{"Water","Ranged"},      fireMode: FireMode.NearestN,          range: 20f, spriteFolder: "WaterDroplet");
        AddWeapon(ws, "Tidal Wave",     "Summons a crashing wave that sweeps through enemy lines.",           Rarity.Rare,   15f, 2.5f,  5, WeaponTrait.Piercing, new List<string>{"Water","Ranged"},      fireMode: FireMode.NearestN,          range: 18f, spriteFolder: "Wave");
        AddWeapon(ws, "Psychic Blast",  "Unleashes a burst of psychic energy at nearby enemies.",             Rarity.Rare,   14f, 1.2f,  2, WeaponTrait.None,     new List<string>{"Magic","Ranged"},      fireMode: FireMode.NearestN,          range: 18f, spriteFolder: "Psychic");

        // --- 2. POPULATE EVOLUTION RECIPES ---
        AddRecipe(ws, "Magician's Wand", "Empty Tome", "Holy Scepter");
        AddRecipe(ws, "Axe", "Heavy Bracers", "Death Spiral");
        AddRecipe(ws, "Pyromancy", "Mana Well", "Supernova");
        AddRecipe(ws, "Longsword", "Pyromancy", "Sword of Fire");
    }

    void AddWeapon(WeaponSystem ws, string name, string desc, Rarity rare, float dmg, float cd, int pierce, WeaponTrait trait, List<string> tags, bool isWeapon = true, FireMode fireMode = FireMode.Default, float range = 0f, int level = 1, float knockback = 0f, string spriteFolder = null, float scale = 5f) {
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
            knockback = knockback,
            spriteFolder = spriteFolder,
            projectileScale = scale
        });
    }

    void AddRecipe(WeaponSystem ws, string wA, string iB, string result) {
        if (ws.recipes.Exists(x => x.resultName == result)) return;
        ws.recipes.Add(new EvolutionRecipe { weaponA = wA, itemB = iB, resultName = result });
    }
}