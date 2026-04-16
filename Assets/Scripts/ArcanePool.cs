using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground-effect circle left by Arcane Arrow on hit.
/// - Sky-blue fill, white outline at outer edge of fill.
/// - Lasts 1.5 seconds.
/// - Ticks 5 damage every 0.5 s to enemies inside.
/// - Doubles all incoming damage (damageTakenMult +1) for enemies inside.
/// </summary>
public class ArcanePool : MonoBehaviour {
    private const float Duration    = 1.5f;
    private const float Radius      = 3.3f;    // world units (medium size, 32% larger than original)
    private const float TickDamage  = 5f;
    private const float TickInterval = 0.5f;
    private const int   Segments    = 48;
    private const float OutlineWidth = 0.08f;

    // Enemies currently boosted by this pool (damageTakenMult increased by 1).
    private readonly HashSet<EnemyEntity> _boosted = new HashSet<EnemyEntity>();

    private float _lifetime;
    private float _tickTimer;

    private static int _activeCount = 0;

    // ── Factory ──────────────────────────────────────────────────────────────
    public static void Spawn(Vector3 worldPos, int maxPools) {
        if (_activeCount >= maxPools) return;  // cap at one pool per weapon level
        var go = new GameObject("ArcanePool");
        go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
        BuildVisuals(go);
        go.AddComponent<ArcanePool>();
    }

    // ── Visuals ──────────────────────────────────────────────────────────────
    static void BuildVisuals(GameObject root) {
        // Fill circle
        var fill = new GameObject("Fill");
        fill.transform.SetParent(root.transform, false);
        var fillLr = fill.AddComponent<LineRenderer>();
        ConfigureCircle(fillLr, Radius, new Color(0.53f, 0.81f, 0.98f, 0.45f), Radius * 2f);
        fillLr.sortingOrder = 3;

        // Outline ring — drawn at Radius*2 so it sits on the outer edge of the fill disc.
        var outline = new GameObject("Outline");
        outline.transform.SetParent(root.transform, false);
        var outlineLr = outline.AddComponent<LineRenderer>();
        ConfigureCircle(outlineLr, Radius * 2f, Color.white, OutlineWidth);
        outlineLr.sortingOrder = 4;
    }

    static void ConfigureCircle(LineRenderer lr, float radius, Color color, float width) {
        lr.positionCount = Segments + 1;
        lr.loop          = true;
        lr.useWorldSpace = false;
        lr.startWidth    = width;
        lr.endWidth      = width;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startColor    = color;
        lr.endColor      = color;
        for (int i = 0; i <= Segments; i++) {
            float t = (float)i / Segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
        }
    }

    // ── Runtime ──────────────────────────────────────────────────────────────
    void Awake() => _activeCount++;

    void Update() {
        _lifetime   += Time.deltaTime;
        _tickTimer  += Time.deltaTime;

        // Sync boost set with enemies currently inside.
        RefreshBoosts();

        // Damage tick.
        if (_tickTimer >= TickInterval) {
            _tickTimer -= TickInterval;
            foreach (var e in _boosted) {
                if (e == null || e.isDead) continue;
                // The 5 damage passes through the existing damageTakenMult (which already includes our ×2).
                e.TakeDamage(TickDamage);
            }
        }

        if (_lifetime >= Duration) {
            RemoveAllBoosts();
            Destroy(gameObject);
        }
    }

    void RefreshBoosts() {
        float radiusSq = Radius * Radius;
        Vector2 center = transform.position;

        // Check enemies that left the circle.
        var toRemove = new List<EnemyEntity>();
        foreach (var e in _boosted) {
            if (e == null || e.isDead || ((Vector2)e.transform.position - center).sqrMagnitude > radiusSq)
                toRemove.Add(e);
        }
        foreach (var e in toRemove) RemoveBoost(e);

        // Add newly entered enemies.
        foreach (var col in Physics2D.OverlapCircleAll(center, Radius)) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead || _boosted.Contains(e)) continue;
            AddBoost(e);
        }
    }

    void AddBoost(EnemyEntity e) {
        e.damageTakenMult += 1f; // +100% = double damage taken
        _boosted.Add(e);
    }

    void RemoveBoost(EnemyEntity e) {
        if (e != null) e.damageTakenMult -= 1f;
        _boosted.Remove(e);
    }

    void RemoveAllBoosts() {
        foreach (var e in _boosted)
            if (e != null) e.damageTakenMult -= 1f;
        _boosted.Clear();
    }

    void OnDestroy() {
        _activeCount--;
        RemoveAllBoosts();
    }
}
