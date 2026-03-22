using System.Collections.Generic;
using UnityEngine;

public enum Rarity { Common, Rare, Epic, Legendary }
public enum WeaponTrait { None, Piercing, Bouncy, Explosive, Homing, Rotating }
public enum CharacterClass { Knight, Mage, Rogue, Necromancer, Paladin, Engineer, Alchemist, Berserker, Ghost, Vampire, Samurai, Bard, Sniper, Monk, Druid, Cyborg, Cleric, Pyromancer, Cryomancer, Merchant }
public enum POIType { Graveyard, Forge, HolyShrine, CursedAltar, ManaWell, MerchantCart, AncientLibrary, HealingSpring, ScrapHeap, VolcanicVent, FrozenObelisk, ThievesDen, Monolith, Beehive, RadarStation, ToxicPit, GoldenStatue, TimeRift, Overgrowth, Meteorite }
public enum EnemyBehavior { Chaser, Charger, Ranged, Tank, Exploder, Shaman, Ghost, Freezer, Swarmer, ShieldBearer, Assassin, Necromancer, BlackHole, Wall, Thief, Commander, Mimic, Sniper, Linker, Vampire, Chrono, Reflector, Burrower, Parasite, Magnet, Puffer, Jockey, Mortar, Siren, Gambler, Webber, Mirror, Battery, Medic, SlimeQueen, WindSpirit, GoldGrit, Hive, Glitch, MimicChest, Sandworm, Silencer, Gravitron, SplitterCell, ParasiteSeed, FogWeaver, Alchemist, MirrorShield, LeadFoot, Finality, Orbit, MagnetRepel, ZigZagger, Bouncer, Anchor, SmokeBomb, Jammer, SniperElite, Flashbanger, Curver, RustAura, XPLeech, Coolant, WeightLifter, TaxCollector, Inverter, ScreenGlitch, Shadow, GhostWriter, LagSpirit, Cheerleader, Totem, Drummer, FlagBearer, Bridge, Landmine, SporeCloud, IceCube, TarPit, Meteor, Paradox, Collector, Reanimator, TimeLooper, MimicUI, Shapeshifter, Pacifist, Drunk, GlassCannon, Echo, Blackout, Puppeteer, Firewall, GreedyKing, Speedster, Juggernaut, SwarmLord, VampireBat, Nemesis, Dev }

[System.Serializable]
public class ItemData {
    public string itemName;
    public string description;
    public List<string> tags; // For Synergy System
    public Rarity rarity;
    public int weight = 10;
    public bool isWeapon;
    public float baseDamage, cooldown;
    public int pierceCount;
    public WeaponTrait trait;
    public GameObject projectilePrefab;
}

[System.Serializable]
public class EvolutionRecipe { public string weaponA; public string itemB; public string resultName; public GameObject evolvedPrefab; }
[System.Serializable]
public class BestiaryEntry { public EnemyBehavior behavior; public int kills; public bool isHunterBonusUnlocked; }
[System.Serializable]
public class NemesisData { public EnemyBehavior killerType; public bool isPendingRevenge; }

[System.Serializable]
public class BiomeData {
    public string biomeName;
    public Color groundColor;
    public List<EnemyBehavior> biomeEnemies;
    public float enemySpeedMultiplier = 1.0f;
    public GameObject environmentalHazard;
}