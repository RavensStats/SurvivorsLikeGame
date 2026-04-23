using System.Collections.Generic;
using UnityEngine;

// Persistent weapon logic for the Aeromancer's Windstorm.
// A single windstorm sprite orbits the player counterclockwise while also
// spinning counterclockwise on its own axis. Any enemy inside the sprite's
// hit area takes damage; the same enemy can only be hit once every 0.5 s.
//
// Level scaling:
//   L2 – damage ×1.25
//   L3 – sprite scale ×1.5  (wider hit area, larger orbit footprint)
//   L4 – damage ×1.25  (cumulative ×1.5625)
//   L5 – sprite scale ×1.5  (cumulative ×2.25)
public class WindstormLogic : MonoBehaviour {
    public ItemData weaponData;

    private const float OrbitRadius    = 2f;
    private const float OrbitSpeed     = 360f;   // degrees/second — 1 full revolution/s CCW
    private const float SelfSpinSpeed  = 720f;   // degrees/second — 2 spins/s CCW
    private const float BaseScale      = 5f;
    private const float DamageInterval = 0.5f;

    private float  _orbitAngle;
    private int    _lastLevel = -1;
    private float  _hitRadius;
    private GameObject _spriteObj;
    private readonly Dictionary<EnemyEntity, float> _lastHitTime = new Dictionary<EnemyEntity, float>();
    private readonly List<EnemyEntity> _staleKeys = new List<EnemyEntity>();

    void Start() {
        BuildSprite();
        ApplyLevelScale();
    }

    void BuildSprite() {
        _spriteObj = new GameObject("WindstormSprite");
        _spriteObj.transform.SetParent(transform, false);

        var sr = _spriteObj.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 7;

        var frames = Resources.LoadAll<Sprite>("Sprites/Weapons/Windstorm");
        if (frames != null && frames.Length > 0) {
            System.Array.Sort(frames, (a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            sr.sprite = frames[0];
            if (frames.Length > 1) {
                var anim = _spriteObj.AddComponent<WeaponSpriteAnimator>();
                anim.Init(frames);
            }
        }
    }

    void ApplyLevelScale() {
        float scale = BaseScale;
        if (weaponData.level >= 3) scale *= 1.5f;
        if (weaponData.level >= 5) scale *= 1.5f;
        _spriteObj.transform.localScale = Vector3.one * scale;
        _hitRadius = scale * 0.5f;
        _lastLevel = weaponData.level;
    }

    void Update() {
        var sms = SurvivorMasterScript.Instance;
        if (sms?.player == null) return;

        transform.position = sms.player.position;

        if (weaponData.level != _lastLevel)
            ApplyLevelScale();

        // Orbit counterclockwise (positive angle = CCW in Unity 2D).
        _orbitAngle = (_orbitAngle + OrbitSpeed * Time.deltaTime) % 360f;
        float rad = _orbitAngle * Mathf.Deg2Rad;
        _spriteObj.transform.localPosition = new Vector3(
            Mathf.Cos(rad) * OrbitRadius,
            Mathf.Sin(rad) * OrbitRadius, 0f);

        // Self-spin counterclockwise.
        _spriteObj.transform.Rotate(0f, 0f, SelfSpinSpeed * Time.deltaTime);

        // Damage enemies inside the sprite's current hit area.
        float now = Time.time;
        float dmg = ComputeDamage(sms);
        var hits = Physics2D.OverlapCircleAll(_spriteObj.transform.position, _hitRadius);
        foreach (var col in hits) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead) continue;
            if (!_lastHitTime.TryGetValue(e, out float last) || now - last >= DamageInterval) {
                _lastHitTime[e] = now;
                e.TakeDamage(dmg);
            }
        }

        // Prune dead/null enemies from the hit-timer dictionary every 120 frames.
        if (Time.frameCount % 120 == 0) {
            _staleKeys.Clear();
            foreach (var kvp in _lastHitTime)
                if (kvp.Key == null || kvp.Key.isDead) _staleKeys.Add(kvp.Key);
            foreach (var k in _staleKeys) _lastHitTime.Remove(k);
        }
    }

    float ComputeDamage(SurvivorMasterScript sms) {
        float dmg = weaponData.baseDamage;
        if (weaponData.level >= 2) dmg *= 1.25f;
        if (weaponData.level >= 4) dmg *= 1.25f;
        return dmg * (sms?.poiDamageMult ?? 1f) * (1f + RunUpgrades.DamageBonus);
    }

    void OnDestroy() {
        if (_spriteObj != null) Destroy(_spriteObj);
    }
}
