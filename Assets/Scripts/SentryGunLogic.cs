using System.Collections.Generic;
using UnityEngine;

// Placed by WeaponSystem.FireSentryGun. Cycles through 8 directional animation frames;
// on each frame change checks for an enemy in that 45° sector and fires a cannonball.
public class SentryGunLogic : MonoBehaviour {
    public ItemData weaponData;

    // Base time (seconds) per frame at L1–L3. L4+ runs 25% faster.
    private const float BaseFrameDuration = 0.3f;

    // 8 directions matching animation frame order (clockwise from East).
    private static readonly Vector2[] Directions = {
        Vector2.right,                        // frame 0 — E
        new Vector2(1f, -1f).normalized,      // frame 1 — SE
        Vector2.down,                         // frame 2 — S
        new Vector2(-1f, -1f).normalized,     // frame 3 — SW
        Vector2.left,                         // frame 4 — W
        new Vector2(-1f, 1f).normalized,      // frame 5 — NW
        Vector2.up,                           // frame 6 — N
        new Vector2(1f, 1f).normalized,       // frame 7 — NE
    };

    private Sprite[] _frames;
    private SpriteRenderer _sr;
    private int _currentFrame;
    private float _frameTimer;

    void Start() {
        _sr = GetComponent<SpriteRenderer>();
        _frames = LoadSentryFrames();
        if (_frames != null && _frames.Length > 0)
            _sr.sprite = _frames[0];
        _currentFrame = 0;
        _frameTimer = 0f;
    }

    void Update() {
        if (weaponData == null) return;

        float frameDuration = weaponData.level >= 4
            ? BaseFrameDuration / 1.25f   // 25% faster at L4+
            : BaseFrameDuration;

        _frameTimer += Time.deltaTime;
        if (_frameTimer < frameDuration) return;

        _frameTimer -= frameDuration;
        _currentFrame = (_currentFrame + 1) % 8;

        if (_frames != null && _currentFrame < _frames.Length)
            _sr.sprite = _frames[_currentFrame];

        CheckAndFire(_currentFrame);
    }

    void CheckAndFire(int frameIdx) {
        if (weaponData == null || SurvivorMasterScript.Instance == null) return;

        Vector2 dir = Directions[frameIdx];
        float range = weaponData.range > 0f ? weaponData.range : 20f;
        float rangeSq = range * range;
        Vector3 pos = transform.position;

        var candidates = SurvivorMasterScript.Instance.Grid.GetNearby(pos);
        EnemyEntity target = null;
        float bestSq = float.MaxValue;

        foreach (var e in candidates) {
            if (e == null || e.isDead) continue;
            Vector2 toEnemy = (Vector2)(e.transform.position - pos);
            float distSq = toEnemy.sqrMagnitude;
            if (distSq > rangeSq || distSq >= bestSq) continue;
            // 22.5° half-angle covers exactly 1/8 of the circle.
            if (Vector2.Angle(dir, toEnemy) <= 22.5f) {
                target = e;
                bestSq = distSq;
            }
        }

        if (target == null) return;

        Vector2 fireDir = ((Vector2)(target.transform.position - pos)).normalized;
        float dmg = weaponData.baseDamage;
        if (weaponData.level >= 2) dmg *= 1.25f;
        dmg *= (SurvivorMasterScript.Instance?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);

        CannonballLogic.Spawn(pos, fireDir, dmg, weaponData.knockback > 0f ? weaponData.knockback : 2f);
    }

    static Sprite[] LoadSentryFrames() {
        var frames = Resources.LoadAll<Sprite>("Sprites/Weapons/SentryGun");
        if (frames != null && frames.Length > 1)
            System.Array.Sort(frames, (a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return frames;
    }
}
