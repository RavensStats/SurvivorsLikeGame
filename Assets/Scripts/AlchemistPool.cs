using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Acid Flask ground effect for the Alchemist.
///
/// Timeline (3 s total):
///   t=0    — spawn, deal initial impact damage to the direct target via ProjectileLogic
///   t=1 s  — first growth (+20 % radius), damage all enemies inside
///   t=2 s  — second growth (+20 % radius), damage all enemies inside
///   t=3 s  — third growth (+20 % radius), damage all enemies inside, then destroy
///
/// Level upgrades:
///   2 — +1 flat damage on every tick
///   3 — base size +25 %
///   4 — enemies inside deal 50 % less damage (damageDealtMult - 0.5)
///   5 — enemies inside attack 50 % slower (attackIntervalMult + 1)
/// </summary>
public class AlchemistPool : MonoBehaviour
{
    private const float Duration       = 3f;
    private const int   GrowthSteps    = 3;
    private const float GrowthInterval = Duration / GrowthSteps; // 1 s
    private const float GrowthFactor   = 1.25f;  // +20 % per pulse
    private const float SpinSpeed      = 45f;    // degrees per second, clockwise

    private float _tickDamage;
    private int   _level;
    private bool  _reducesDamage;
    private bool  _reducesAttackSpeed;

    private SpriteRenderer _sr;
    private float _elapsed;
    private float _growthTimer;
    private int   _growthsDone;
    private float _currentRadius;   // world-space radius, updated on each growth

    private readonly HashSet<EnemyEntity> _debuffed = new HashSet<EnemyEntity>();

    // ── Factory ───────────────────────────────────────────────────────────────
    public static void Spawn(Vector3 worldPos, int level, float baseDamage)
    {
        var go = new GameObject("AlchemistPool");
        go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

        float sizeScale   = level >= 3 ? 1.25f : 1f;
        float objectScale = 30f * sizeScale;
        go.transform.localScale = Vector3.one * objectScale;

        Sprite spr = Resources.Load<Sprite>("Sprites/Weapons/PoisonPool/PoisonPool");

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.sortingOrder = 3;
        sr.color        = new Color(0.25f, 0.9f, 0.1f, 0.8f);

        var pool               = go.AddComponent<AlchemistPool>();
        pool._sr               = sr;
        pool._level            = level;
        pool._tickDamage       = baseDamage + (level >= 2 ? 1f : 0f);
        pool._reducesDamage    = level >= 4;
        pool._reducesAttackSpeed = level >= 5;

        // Derive initial world-space radius from sprite bounds + object scale
        float sprExtent       = spr != null ? spr.bounds.extents.x : 0.5f;
        pool._currentRadius   = sprExtent * objectScale;
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        // Slowly spin clockwise (negative Z = clockwise in Unity 2D)
        transform.Rotate(0f, 0f, -SpinSpeed * Time.deltaTime);

        _elapsed     += Time.deltaTime;
        _growthTimer += Time.deltaTime;

        // Growth pulses — one per second, three total
        if (_growthsDone < GrowthSteps && _growthTimer >= GrowthInterval)
        {
            _growthTimer -= GrowthInterval;
            _growthsDone++;

            // Scale up
            transform.localScale *= GrowthFactor;

            // Recalculate radius after scale change
            Sprite spr      = _sr != null ? _sr.sprite : null;
            float sprExtent  = spr != null ? spr.bounds.extents.x : 0.5f;
            _currentRadius   = sprExtent * transform.localScale.x;

            // Damage tick to all enemies inside the new, larger area
            DamageAllInside();
        }

        // Continuously track debuff membership (level 4+ only)
        if (_reducesDamage || _reducesAttackSpeed)
            RefreshDebuffs();

        // Destroy on expiry
        if (_elapsed >= Duration)
        {
            RemoveAllDebuffs();
            Destroy(gameObject);
        }
    }

    void OnDestroy() => RemoveAllDebuffs();

    // ── Damage ────────────────────────────────────────────────────────────────
    void DamageAllInside()
    {
        var sms = SurvivorMasterScript.Instance;
        if (sms == null) return;
        Vector2 center  = transform.position;
        float   radiusSq = _currentRadius * _currentRadius;
        foreach (var e in sms.Grid.GetNearby(transform.position))
        {
            if (e == null || e.isDead) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude <= radiusSq)
                e.TakeDamage(_tickDamage);
        }
    }

    // ── Debuffs ───────────────────────────────────────────────────────────────
    void RefreshDebuffs()
    {
        Vector2 center   = transform.position;
        float   radiusSq = _currentRadius * _currentRadius;

        // Remove enemies that have left the pool
        var toRemove = new List<EnemyEntity>();
        foreach (var e in _debuffed)
        {
            if (e == null || e.isDead || ((Vector2)e.transform.position - center).sqrMagnitude > radiusSq)
                toRemove.Add(e);
        }
        foreach (var e in toRemove) RemoveDebuff(e);

        // Add newly entered enemies
        foreach (var col in Physics2D.OverlapCircleAll(center, _currentRadius))
        {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead || _debuffed.Contains(e)) continue;
            AddDebuff(e);
        }
    }

    void AddDebuff(EnemyEntity e)
    {
        if (_reducesDamage)      e.damageDealtMult    -= 0.5f;
        if (_reducesAttackSpeed) e.attackIntervalMult += 1f;
        _debuffed.Add(e);
    }

    void RemoveDebuff(EnemyEntity e)
    {
        if (e != null)
        {
            if (_reducesDamage)      e.damageDealtMult    += 0.5f;
            if (_reducesAttackSpeed) e.attackIntervalMult -= 1f;
        }
        _debuffed.Remove(e);
    }

    void RemoveAllDebuffs()
    {
        foreach (var e in _debuffed) RemoveDebuff(e);
        _debuffed.Clear();
    }
}
