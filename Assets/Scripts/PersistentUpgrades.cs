using UnityEngine;

/// <summary>
/// Reads persistent upgrade levels from PlayerPrefs and exposes runtime bonuses.
/// Read by PlayerMovement, SurvivorMasterScript, and WeaponSystem at game start.
/// </summary>
public static class PersistentUpgrades {
    public static float SpeedBonus      => PlayerPrefs.GetInt("upg_speed",    0) * 1f;   // +1 per level
    public static float MaxHPBonus      => PlayerPrefs.GetInt("upg_hp",       0) * 10f;  // +10 per level
    public static float CooldownMult    => 1f - PlayerPrefs.GetInt("upg_cooldown", 0) * 0.05f; // -5% per level
    public static float XPRateMult      => 1f + PlayerPrefs.GetInt("upg_xprate",  0) * 0.10f; // +10% per level
    public static bool  IsVIP {
        get {
            long expiry = long.Parse(PlayerPrefs.GetString("vip_expiry", "0"));
            return expiry > System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
