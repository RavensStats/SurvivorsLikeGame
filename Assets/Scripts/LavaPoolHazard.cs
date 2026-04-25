using UnityEngine;

/// <summary>
/// Ground hazard spawned by weapons with the Magmatic enhancement.
/// Damages enemies standing inside for 10 seconds, then despawns.
/// Does not damage the player.
/// </summary>
public class LavaPoolHazard : MonoBehaviour
{
    private float _damagePerSecond;
    private float _duration;
    private float _elapsed;
    private float _tickTimer;
    private const float TickInterval = 1f;

    private SpriteRenderer _sr;
    private float _worldRadiusSq;

    public static void Spawn(Vector3 worldPos, float damagePerSecond, float duration)
    {
        var go = new GameObject("LavaPool");
        go.transform.position   = new Vector3(worldPos.x, worldPos.y, 0f);
        go.transform.localScale = Vector3.one * 8f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 3;
        sr.color        = new Color(1f, 0.38f, 0f, 0.75f);

        Sprite spr = Resources.Load<Sprite>("Sprites/Weapons/BloodPool/BloodPool");
        if (spr == null) spr = CreateCircleSprite(32);
        sr.sprite = spr;

        float sprExtent = (spr != null) ? spr.bounds.extents.x : 0.5f;
        float worldRadius = sprExtent * 8f;

        var h = go.AddComponent<LavaPoolHazard>();
        h._sr              = sr;
        h._damagePerSecond = damagePerSecond;
        h._duration        = duration;
        h._worldRadiusSq   = worldRadius * worldRadius;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        if (_sr != null && _elapsed > _duration - 1.5f) {
            Color c = _sr.color;
            c.a = Mathf.Clamp01((_duration - _elapsed) / 1.5f) * 0.75f;
            _sr.color = c;
        }

        if (_elapsed >= _duration) { Destroy(gameObject); return; }

        _tickTimer += Time.deltaTime;
        if (_tickTimer < TickInterval) return;
        _tickTimer = 0f;

        Vector2 center = transform.position;
        var sms = SurvivorMasterScript.Instance;
        if (sms == null) return;

        WeaponSystem.EnhancementDepth++;
        foreach (var e in sms.Grid.GetNearby(transform.position)) {
            if (e == null || e.isDead) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude <= _worldRadiusSq)
                e.TakeDamage(_damagePerSecond);
        }
        WeaponSystem.EnhancementDepth--;
    }

    static Sprite CreateCircleSprite(int res)
    {
        var tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float half = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++) {
                float dx = x - half, dy = y - half;
                tex.SetPixel(x, y, (dx*dx + dy*dy) <= half*half ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
