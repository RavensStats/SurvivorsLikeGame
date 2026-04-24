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
                    : cls == CharacterClass.Cleric           ? "Conversion Circle"
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
                    : cls == CharacterClass.ArcticScout      ? "Blizzard"
                    : cls == CharacterClass.Dwarf            ? "War Hammer"
                    : cls == CharacterClass.Hydromancer      ? "Downpour"
                    : cls == CharacterClass.Hydrokineticist  ? "Tidal Wave"
                    : cls == CharacterClass.Battlemage       ? "Spirit Aura"
                    : cls == CharacterClass.Psychomancer     ? "Psychic Blast"
                    : cls == CharacterClass.GravityManipulator ? "Gravity Well"
                    : cls == CharacterClass.Aeromancer        ? "Windstorm"
                    : cls == CharacterClass.Geomancer        ? "Meteor Strike"
                    : cls == CharacterClass.Hivemaster       ? "Insect Swarm"
                    : cls == CharacterClass.Psammomancer    ? "Sand Shield"
                    : cls == CharacterClass.DimensionMaster ? "Teleportation"
                    : null;
        if (name == null) return;

        ItemData weapon = ws.cardPool.Find(w => w.itemName == name);
        if (weapon == null) { Debug.LogWarning($"[ZenithDatabaseLoader] Starting weapon '{name}' not found in pool."); return; }

        ws.activeWeapons.Add(weapon);

        // PuppetMaster: also starts with one random weapon from the pool in addition to Shadow Clone.
        if (cls == CharacterClass.PuppetMaster) {
            var bonusPool = new List<ItemData>();
            foreach (var item in ws.cardPool)
                if (item.isWeapon && item.itemName != "Shadow Clone" && !ws.activeWeapons.Contains(item))
                    bonusPool.Add(item);
            if (bonusPool.Count > 0)
                ws.activeWeapons.Add(bonusPool[Random.Range(0, bonusPool.Count)]);
        }
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
        AddWeapon(ws, "Magician's Wand", "Fires a bolt at the nearest N enemies (N = level).",               Rarity.Common, 20f, 1.2f, 1,  WeaponTrait.None,     new List<string>{"Magic", "Projectiles"},  fireMode: FireMode.NearestN,      knockback: 1f, spriteFolder: "Wand", scale: 10f);
        AddWeapon(ws, "Hunter's Bow",    "Fires an arrow at up to N random enemies in range (N = level).",   Rarity.Common, 8f,  1.8f, 1,  WeaponTrait.None,     new List<string>{"Physical", "Ranged"},    fireMode: FireMode.RangerArrow,    range: 35f,  spriteFolder: "Arrow", scale: 10f);
        AddWeapon(ws, "Longsword",        "Slashes nearby enemies in a quick arc.",                           Rarity.Common, 12f, 0.8f, 1,  WeaponTrait.None,     new List<string>{"Physical", "Melee"},    fireMode: FireMode.ArcSwing,       range: 12f, spriteFolder: "Sword");
        AddWeapon(ws, "Axe",              "Hurls an axe in a parabolic arc at a random enemy — damage scales with missing HP. Returns to the player after hitting or missing, then throws again immediately.", Rarity.Common, 15f, 2.0f, 1, WeaponTrait.None, new List<string>{"Physical", "Melee"}, fireMode: FireMode.BerserkerAxe, spriteFolder: "Axe");
        AddWeapon(ws, "Pyromancy",        "Orbits the player, burning enemies.",                              Rarity.Rare,   8f,  3.0f, 99, WeaponTrait.Rotating, new List<string>{"Fire", "Magic"},       fireMode: FireMode.Orbit, spriteFolder: "Flame");
        AddWeapon(ws, "Empty Tome",   "Increases cooldown speed of all weapons.",                          Rarity.Common, 0,   0,    0,  WeaponTrait.None,     new List<string>{"Utility", "Magic"},    false);
        AddWeapon(ws, "Heavy Bracers","Increases knockback power.",                                        Rarity.Common, 0,   0,    0,  WeaponTrait.None,     new List<string>{"Heavy", "Physical"},   false);

        // ── Batch 1: Rogue → Monk ────────────────────────────────────────────
        AddWeapon(ws, "Blowgun",        "Fires a fast dart at the nearest enemy.",                         Rarity.Common, 10f, 0.35f, 1, WeaponTrait.None,     new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,  range: 15f, spriteFolder: "Dart", scale: 10f);
        AddWeapon(ws, "Summon Undead", "Raises fallen enemies as permanent allies — the more of a type you've killed, the more likely it is summoned.", Rarity.Rare, 18f, 10f, 0, WeaponTrait.None, new List<string>{"Magic","Necromancy"}, fireMode: FireMode.SummonUndead, spriteFolder: "Summon");
        AddWeapon(ws, "Sword of the Heavens", "A blessed arc swing that channels divine energy through enemies.", Rarity.Common, 22f, 1.8f,  2, WeaponTrait.None,      new List<string>{"Physical","Melee","Holy"}, fireMode: FireMode.HolySword, range: 15f, knockback: 6f, spriteFolder: "HolySword");
        AddWeapon(ws, "Sentry Gun",     "Deploys a turret that scans for enemies and fires cannonballs.", Rarity.Rare,   8f,  0.3f,  1, WeaponTrait.None,     new List<string>{"Physical","Ranged"},   fireMode: FireMode.SentryGun, range: 20f, knockback: 2f, spriteFolder: "SentryGun");
        AddWeapon(ws, "Acid Flask",     "Thrown flask that shatters, leaving lingering acid.",             Rarity.Common, 12f, 1.5f,  1, WeaponTrait.Explosive,new List<string>{"Poison","Ranged"},     fireMode: FireMode.PoisonPool, range: 18f, spriteFolder: "PoisonPool");
        AddWeapon(ws, "Gold Coin",      "Tosses sharp coins that ricochet between up to 3 enemies.",       Rarity.Common, 9f,  0.6f,  3, WeaponTrait.Bouncy,   new List<string>{"Physical","Ranged"},   fireMode: FireMode.NearestN,  spriteFolder: "Misc/GoldCoin");
        AddWeapon(ws, "Spectral Beam",  "Pierces every enemy in a straight line.",                         Rarity.Rare,   15f, 1.4f,  999,WeaponTrait.Piercing,new List<string>{"Magic","Ranged"},      fireMode: FireMode.SpectralBeam, range: 25f, spriteFolder: "SpectralBeam");
        AddWeapon(ws, "Blood Pool",     "Summons a vampiric blood pool that drains life from all enemies within it.", Rarity.Rare, 14f, 7.0f, 99, WeaponTrait.None, new List<string>{"Physical","Melee"}, fireMode: FireMode.BloodPool, range: 12f, spriteFolder: "BloodPool");
        AddWeapon(ws, "Katana",         "Slashes in a swift 90° arc around the player.",                   Rarity.Rare,   20f, 1.0f,  5, WeaponTrait.Piercing, new List<string>{"Physical","Melee"},    fireMode: FireMode.KatanaSlash, range: 15f, spriteFolder: "Katana");
        AddWeapon(ws, "Ballad",            "Fires a music note in a sinusoidal wave that charms enemies on hit.", Rarity.Common, 0f,  2.5f,  99, WeaponTrait.None, new List<string>{"Magic","Support"},     fireMode: FireMode.BalladWave, spriteFolder: "MusicNote");
        AddWeapon(ws, "Sniper Rifle",      "A laser reticle patrols the mid-range zone and locks onto enemies.", Rarity.Rare, 35f, 1.0f, 3, WeaponTrait.Piercing, new List<string>{"Physical","Ranged"},   fireMode: FireMode.SniperReticle, range: 60f, spriteFolder: "SniperReticle");
        AddWeapon(ws, "Fist of Absolution","Rapid punches that briefly stun the target.",                     Rarity.Common, 8f,  2.5f,  1, WeaponTrait.None,     new List<string>{"Physical","Melee"},    fireMode: FireMode.RisingFist, range: 2.5f, spriteFolder: "Fist");
        AddWeapon(ws, "Gravity Well",      "Expands a gravity ring that damages enemies on expansion and collapse, pulling them inward.", Rarity.Rare, 20f, 7.0f, 99, WeaponTrait.None, new List<string>{"Magic","AoE"}, fireMode: FireMode.GravityWell, range: 8f);
        AddWeapon(ws, "Windstorm",         "A spinning windstorm orbits the character counterclockwise, striking nearby enemies every 0.5 s.", Rarity.Common, 8f, 1.0f, 99, WeaponTrait.None, new List<string>{"Nature","Melee"}, fireMode: FireMode.Windstorm);
        AddWeapon(ws, "Meteor Strike",     "Calls meteors from the sky onto random enemies, stunning them on impact.", Rarity.Rare, 30f, 5.0f, 1, WeaponTrait.None, new List<string>{"Physical","Ranged"}, fireMode: FireMode.MeteorStrike, range: 40f, spriteFolder: "MeteorStrike");
        AddWeapon(ws, "Insect Swarm",      "Spawns a swarm of insects that hunt down enemies and sting them repeatedly.", Rarity.Common, 8f, 5.0f, 99, WeaponTrait.None, new List<string>{"Nature","Melee"}, fireMode: FireMode.InsectSwarm, spriteFolder: "InsectSwarm");

        // ── Batch 2: Druid → PuppetMaster ────────────────────────────────────
        AddWeapon(ws, "Creeping Vines",  "Launches a vine at the nearest enemy; on hit it chains to the next closest, spreading across the battlefield.",            Rarity.Common, 11f, 8.0f,  1, WeaponTrait.None,     new List<string>{"Nature","Ranged"},     fireMode: FireMode.CreepingVines, spriteFolder: "Vines");
        AddWeapon(ws, "Radial Laser",   "Spins around the player, striking any enemy it touches, then fires a piercing red laser at the closest enemy.", Rarity.Rare, 13f, 3.0f, 1, WeaponTrait.None, new List<string>{"Energy","Ranged"}, fireMode: FireMode.RadialLaser, spriteFolder: "RadialLaser");
        AddWeapon(ws, "Holy Staff",     "Pillar of light that damages enemies and briefly heals the player.", Rarity.Rare, 16f, 1.5f, 1, WeaponTrait.None,     new List<string>{"Holy","Magic"},        fireMode: FireMode.NearestN,  spriteFolder: "HolyStaff");
        AddWeapon(ws, "Ice Shard",      "Hurls a shard at the nearest enemy — on impact it slows the target and fans 3 smaller shards behind it.", Rarity.Common, 10f, 7.0f, 1, WeaponTrait.None, new List<string>{"Ice","Ranged"}, fireMode: FireMode.IceShard, spriteFolder: "IceShard");
        AddWeapon(ws, "Chain Lightning","Arcing electricity that chains between nearby enemies.",           Rarity.Rare,   20f, 2.0f,  1, WeaponTrait.Explosive,new List<string>{"Electric","Magic"},   fireMode: FireMode.ChainLightning, range: 25f, spriteFolder: "ChainLightning");
        AddWeapon(ws, "Assassin Blade", "Stab twice then slash — each hit poisons the target for 50% damage per second.", Rarity.Rare, 14f, 1.0f, 1, WeaponTrait.None, new List<string>{"Physical","Melee"}, fireMode: FireMode.PoisonDagger, range: 3f, spriteFolder: "PoisonDagger");
        AddWeapon(ws, "Trident",        "Stabs nearby enemies in the densest direction, or hurls the trident to slow a distant foe.", Rarity.Rare, 18f, 2.5f, 1, WeaponTrait.None, new List<string>{"Physical","Melee"}, fireMode: FireMode.TridentStrike, range: 5f, spriteFolder: "Trident");
        AddWeapon(ws, "Wolf Claws",     "Two rapid claw swipes at the nearest enemy — each swipe deals damage and inflicts bleeding.", Rarity.Common, 9f, 0.3f, 1, WeaponTrait.None, new List<string>{"Physical","Melee"}, fireMode: FireMode.WolfClaws, spriteFolder: "WolfClaws");
        AddWeapon(ws, "Time Manipulation", "Conjures a clock face that spins to a random hour — each hour triggers a unique temporal effect.", Rarity.Rare, 0f, 8.0f, 1, WeaponTrait.None, new List<string>{"Magic","Time"}, fireMode: FireMode.TemporalManipulation, range: 5f);
        AddWeapon(ws, "Void Orb",       "Gravity-well projectile that pulls nearby enemies on impact.",    Rarity.Rare,   14f, 1.8f,  1, WeaponTrait.Explosive,new List<string>{"Dark","Magic"},        fireMode: FireMode.VoidOrb, spriteFolder: "VoidOrb");
        AddWeapon(ws, "Scythe",         "Massive scythe orbits the player clockwise — spawns a skeleton on every kill.", Rarity.Rare,   22f, 5.0f,  4, WeaponTrait.Piercing, new List<string>{"Physical","Melee"},    fireMode: FireMode.ScytheOrbit, range: 14f, spriteFolder: "Scythe");
        AddWeapon(ws, "Arcane Arrow",   "Arrows leave a magic dust trail dealing damage over time.",       Rarity.Rare,   10f, 1.0f,  2, WeaponTrait.Piercing, new List<string>{"Magic","Ranged"},      fireMode: FireMode.RandomInRange, range: 40f, spriteFolder: "ArcaneArrow");
        AddWeapon(ws, "Plague Canister","Lobs a canister in a rotating cardinal direction; the cloud lingers 5 s, dealing damage and causing enemies to miss 25% of attacks.", Rarity.Rare, 12f, 3.0f, 1, WeaponTrait.None, new List<string>{"Poison","Ranged"}, fireMode: FireMode.PoisonGasCloud, range: 10f, spriteFolder: "PoisonGas");
        AddWeapon(ws, "Dual Revolvers", "Fans cannonballs in a left-side arc, firing pairs in quick bursts.", Rarity.Common, 12f, 2.0f, 1, WeaponTrait.None, new List<string>{"Physical","Ranged"},   fireMode: FireMode.DualRevolvers, range: 25f);
        AddWeapon(ws, "Throwing Axe",   "Hurls a spinning axe at the nearest enemy, slowing them on impact.", Rarity.Common, 14f, 2.0f, 1, WeaponTrait.None, new List<string>{"Physical","Ranged"},   fireMode: FireMode.WoodcutterAxe, range: 20f, spriteFolder: "WoodcutterAxe");
        AddWeapon(ws, "Scrap Maul",     "Heavy melee weapon — smashes both sides of the player on each swing.", Rarity.Common, 18f, 5.0f, 1, WeaponTrait.None, new List<string>{"Physical","Melee"}, fireMode: FireMode.ScrapMaul, range: 4f, spriteFolder: "ScrapMaul");
        AddWeapon(ws, "Oracle Beam",    "Beams of light strike random nearby foes simultaneously.",        Rarity.Rare,   14f, 1.2f,  1, WeaponTrait.None,     new List<string>{"Holy","Magic"},        fireMode: FireMode.OracleBeam, range: 28f, spriteFolder: "OracleBeam");
        AddWeapon(ws, "Shadow Clone",   "Spawns a grey mirror clone that mimics all your weapon attacks — equipped with copies of every weapon you carry.", Rarity.Rare, 0f, 99f, 0, WeaponTrait.None, new List<string>{"Dark","Magic"}, fireMode: FireMode.ShadowClone);

        // ── Pool-only weapons ──────────────────────────────────────────────────
        AddWeapon(ws, "Spirit Aura",    "A swirling spiritual ring that orbits and burns nearby enemies.",    Rarity.Rare,   6f,  2.8f, 99, WeaponTrait.Rotating, new List<string>{"Magic","Spirit"},      fireMode: FireMode.MagicAura,     range: 6f,  spriteFolder: "Aura");
        AddWeapon(ws, "Caltrop Throw",  "Hurls caltrops in arcing throws; they linger until an enemy steps on them.", Rarity.Common, 10f, 1.5f, 2, WeaponTrait.None, new List<string>{"Physical","Ranged"},   fireMode: FireMode.CaltropThrow,      range: 18f, spriteFolder: "Caltrop");

        // ── New character starting weapons ─────────────────────────────────────
        AddWeapon(ws, "Binding Circle", "Conjures a runic circle under the nearest enemy — rooting and damaging anything inside it.", Rarity.Rare, 12f, 4.0f, 1, WeaponTrait.None, new List<string>{"Magic","Support"}, fireMode: FireMode.BindingCircle, range: 16f, spriteFolder: "BindingCircle");
        AddWeapon(ws, "Sand Shield",    "Conjures a sand barrier around the player, blocking all damage and reflecting it at attackers.", Rarity.Rare, 18f, 6.0f, 1, WeaponTrait.None, new List<string>{"Sand","Defense"}, fireMode: FireMode.SandShield, spriteFolder: "SandShield");
        AddWeapon(ws, "Conversion Circle", "A holy circle that permanently converts normal enemies to allies after 5 seconds inside.", Rarity.Rare, 1f, 1.0f, 0, WeaponTrait.None, new List<string>{"Holy","Support"}, fireMode: FireMode.ConversionCircle, range: 5f, spriteFolder: "ConversionCircle");
        AddWeapon(ws, "Saw Blade",      "Launches a spinning saw blade that bounces at 45° off each enemy it hits until it leaves the screen.", Rarity.Common, 14f, 1.2f, 1, WeaponTrait.None, new List<string>{"Physical","Ranged"}, fireMode: FireMode.SawBlade, range: 30f, spriteFolder: "SawBlade");
        AddWeapon(ws, "Shuriken",       "Hurls a volley of spinning shuriken in a left-side arc; each despawns on first hit.", Rarity.Common, 8f, 1.5f, 1, WeaponTrait.None, new List<string>{"Physical","Ranged"}, fireMode: FireMode.ShurikenThrow, spriteFolder: "Shuriken");
        AddWeapon(ws, "Blizzard",        "Sweeps 20 wind-blown snowflakes across the screen, damaging and slowing every enemy they touch.", Rarity.Common, 10f, 15f, 1, WeaponTrait.None, new List<string>{"Ice","Ranged"}, fireMode: FireMode.Blizzard, spriteFolder: "Snowflake");
        AddWeapon(ws, "War Hammer",     "Hurled high into the air, it craters on landing — damaging enemies in the impact zone and stunning all nearby for 1 second.", Rarity.Common, 20f, 2.0f, 1, WeaponTrait.None, new List<string>{"Physical","Melee"}, fireMode: FireMode.WarHammer, spriteFolder: "WarHammer");
        AddWeapon(ws, "Downpour",        "Calls forth a downpour — water droplets rain across the screen, dealing damage on contact.",  Rarity.Common, 9f,  5.0f,  1, WeaponTrait.None,     new List<string>{"Water","Ranged"},      fireMode: FireMode.Downpour,          spriteFolder: "WaterDroplet");
        AddWeapon(ws, "Tidal Wave",     "Summons a crashing wave that sweeps through enemy lines.",           Rarity.Rare,   15f, 2.5f,  5, WeaponTrait.Piercing, new List<string>{"Water","Ranged"},      fireMode: FireMode.TidalWave, range: 18f, spriteFolder: "Wave");
        AddWeapon(ws, "Psychic Blast",  "Unleashes a burst of psychic energy at nearby enemies.",             Rarity.Rare,   14f, 1.2f,  2, WeaponTrait.None,     new List<string>{"Magic","Ranged"},      fireMode: FireMode.AnimatedStrike, range: 18f, spriteFolder: "Psychic");
        AddWeapon(ws, "Teleportation",  "Blinks through enemies one by one — each strike deals damage; other weapons fire from each strike position.", Rarity.Common, 18f, 8.0f, 1, WeaponTrait.None, new List<string>{"Magic","Melee"}, fireMode: FireMode.Teleportation, spriteFolder: "Teleportation");

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