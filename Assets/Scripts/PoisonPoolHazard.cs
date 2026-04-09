using UnityEngine;

/// <summary>
/// Ground hazard left after a Puffer explodes.
/// Uses the PoisonPool weapon sprite, damages both the player and any enemies
/// standing inside the area.  Lasts 5-10 seconds, then fades and self-destructs.
/// </summary>
public class PoisonPoolHazard : MonoBehaviour
{
    private float _damagePerSecond;
    private float _duration;
    private float _elapsed;
    private float _tickTimer;
    private const float TickInterval = 0.5f;

    private SpriteRenderer _sr;
    private float _worldRadiusSq;

    // ── Factory ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Spawn a poison pool at <paramref name="worldPos"/>.
    /// <paramref name="poolScale"/> is applied directly to the GameObject's local scale;
    /// the sprite fills that scale, and the damage area matches the sprite exactly.
    /// </summary>
    public static void Spawn(Vector3 worldPos, float poolScale, float damagePerSecond)
    {
        GameObject go = new GameObject("PufferPoisonPool");
        go.transform.position  = new Vector3(worldPos.x, worldPos.y, 0f);
        go.transform.localScale = Vector3.one * poolScale;

        Sprite spr = Resources.Load<Sprite>("Sprites/Weapons/PoisonPool/PoisonPool");

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = spr;
        sr.sortingOrder = 3;   // above ground tiles, below enemies
        sr.color        = new Color(0.3f, 1f, 0.15f, 0.72f);  // toxic green, semi-transparent

        // World-space radius = sprite half-extent (local) × GO scale
        float sprLocalExtent = (spr != null) ? spr.bounds.extents.x : 0.5f;
        float worldRadius    = sprLocalExtent * poolScale;

        PoisonPoolHazard h = go.AddComponent<PoisonPoolHazard>();
        h._sr               = sr;
        h._damagePerSecond  = damagePerSecond;
        h._duration         = Random.Range(5f, 10f);
        h._worldRadiusSq    = worldRadius * worldRadius;
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        _elapsed += Time.deltaTime;

        // Fade out over the last second
        if (_sr != null && _elapsed > _duration - 1f)
        {
            Color c = _sr.color;
            c.a = Mathf.Clamp01(_duration - _elapsed) * 0.72f;
            _sr.color = c;
        }

        if (_elapsed >= _duration) { Destroy(gameObject); return; }

        // Damage tick
        _tickTimer += Time.deltaTime;
        if (_tickTimer < TickInterval) return;
        _tickTimer = 0f;

        ApplyDamageTick(_damagePerSecond * TickInterval);
    }

    void ApplyDamageTick(float dmg)
    {
        Vector2 center = transform.position;
        var sms = SurvivorMasterScript.Instance;
        if (sms == null) return;

        // Damage player
        if (sms.player != null)
        {
            if (((Vector2)sms.player.position - center).sqrMagnitude <= _worldRadiusSq)
                sms.TakeDamage(dmg);
        }

        // Damage enemies inside the pool
        foreach (var e in sms.Grid.GetNearby(transform.position))
        {
            if (e == null || e.isDead) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude <= _worldRadiusSq)
                e.TakeDamage(dmg);
        }
    }
}
