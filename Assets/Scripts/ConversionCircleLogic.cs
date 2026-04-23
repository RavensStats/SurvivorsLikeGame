using System.Collections.Generic;
using UnityEngine;

// Persistent holy circle for the Cleric's Conversion Circle weapon.
// Always visible; follows the player. Normal-tier enemies that remain inside
// for long enough are permanently charmed. If only Elite/Boss enemies are alive
// on screen, the accumulated charmed-enemy count deals damage every 3 s.
public class ConversionCircleLogic : MonoBehaviour {
    public ItemData weaponData;

    // Counts permanently converted enemies across the entire run; reset by ResetForNewRun.
    public static int CharmedCount = 0;

    private SpriteRenderer _sr;
    private readonly Dictionary<EnemyEntity, float> _timers     = new Dictionary<EnemyEntity, float>();
    private readonly List<EnemyEntity>               _inCircle   = new List<EnemyEntity>();
    private readonly List<EnemyEntity>               _toRemove   = new List<EnemyEntity>();
    private float _dmgAccum;

    private const float DmgInterval = 3f;

    public void Init(Sprite spr) {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null && spr != null) _sr.sprite = spr;
    }

    void Update() {
        var sms = SurvivorMasterScript.Instance;
        if (sms?.player == null) return;

        // Follow the player.
        transform.position = sms.player.position;

        // Update scale so size reflects the current weapon level.
        float radius = GetRadius();
        transform.localScale = Vector3.one * (radius * 2f);

        float threshold = weaponData.level >= 5 ? 3f
                        : weaponData.level >= 2 ? 4f
                        : 5f;

        // Gather normal-tier enemies currently inside the circle.
        _inCircle.Clear();
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius)) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead || e.isPermanentlyCharmed) continue;
            if (e.tier == EnemyTier.Elite || e.tier == EnemyTier.Boss) continue;
            _inCircle.Add(e);
        }

        // Reset timers for enemies that left the circle.
        _toRemove.Clear();
        foreach (var kvp in _timers)
            if (kvp.Key == null || kvp.Key.isDead || !_inCircle.Contains(kvp.Key))
                _toRemove.Add(kvp.Key);
        foreach (var e in _toRemove) _timers.Remove(e);

        // Tick timers and convert on threshold.
        foreach (var e in _inCircle) {
            if (!_timers.ContainsKey(e)) _timers[e] = 0f;
            _timers[e] += Time.deltaTime;
            if (_timers[e] >= threshold) {
                _timers.Remove(e);
                e.isPermanentlyCharmed = true;
                CharmedCount++;
            }
        }

        // Elite/Boss-only screen → deal charmed-count damage every 3 s.
        _dmgAccum += Time.deltaTime;
        if (_dmgAccum >= DmgInterval) {
            _dmgAccum -= DmgInterval;
            if (CharmedCount > 0 && OnlyElitesOrBossesOnScreen(sms)) {
                foreach (var col in Physics2D.OverlapCircleAll(sms.player.position, 200f)) {
                    if (!col.CompareTag("Enemy")) continue;
                    var e = col.GetComponent<EnemyEntity>();
                    if (e == null || e.isDead || e.isPermanentlyCharmed) continue;
                    if (!SurvivorMasterScript.IsOnScreen(e.transform.position)) continue;
                    e.TakeDamage(CharmedCount);
                }
            }
        }
    }

    float GetRadius() {
        float r = (weaponData != null && weaponData.range > 0f) ? weaponData.range : 5f;
        if (weaponData != null) {
            if (weaponData.level >= 3) r *= 1.10f;
            if (weaponData.level >= 4) r *= 1.15f;
        }
        return r;
    }

    static bool OnlyElitesOrBossesOnScreen(SurvivorMasterScript sms) {
        foreach (var e in sms.Grid.GetNearby(sms.player.position)) {
            if (e == null || e.isDead || e.isPermanentlyCharmed) continue;
            if (!SurvivorMasterScript.IsOnScreen(e.transform.position)) continue;
            if (e.tier == EnemyTier.Normal) return false;
        }
        return true;
    }
}
