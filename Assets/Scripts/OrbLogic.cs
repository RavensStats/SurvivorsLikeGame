using UnityEngine;

/// <summary>
/// Attached to orb GameObjects spawned by WeaponSystem for the Orbit fire mode.
/// Rotates around the player at a fixed radius and deals periodic damage to nearby enemies.
/// </summary>
public class OrbLogic : MonoBehaviour {
    public float orbitRadius    = 3f;
    public float orbitSpeed     = 120f;   // degrees per second
    public float startAngle;              // initial angle in degrees
    public float damage;
    public float damageRadius   = 1.2f;
    public float damageInterval = 0.75f;

    private float nextDamageTick;

    void Start() {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
            sr.sprite = MakeCircleSprite(16);
    }

    void Update() {
        var player = SurvivorMasterScript.Instance?.player;
        if (player == null) return;

        startAngle += orbitSpeed * Time.deltaTime;
        float rad = startAngle * Mathf.Deg2Rad;
        transform.position = (Vector2)player.position
            + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;

        if (Time.time < nextDamageTick) return;
        nextDamageTick = Time.time + damageInterval;

        var enemies = SurvivorMasterScript.Instance.Grid.GetNearby(transform.position);
        float radSq = damageRadius * damageRadius;
        foreach (var e in enemies)
            if (e != null && !e.isDead
                && (e.transform.position - transform.position).sqrMagnitude <= radSq)
                e.TakeDamage(damage);
    }

    static Sprite MakeCircleSprite(int res) {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float half = res * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++) {
                float dx = x - half, dy = y - half;
                tex.SetPixel(x, y, (dx * dx + dy * dy) <= half * half ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
