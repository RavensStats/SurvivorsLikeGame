using System.Collections.Generic;
using UnityEngine;

// Persistent ground circle for the Enchanter's Binding Circle weapon.
// Fades in over 0.5 s, lingers for the configured lifetime, then fades out over 0.5 s.
// Enemies inside are rooted (movement zeroed via EnemyEntity.rootedStacks) and take
// damage every 3 seconds.
public class BindingCircleLogic : MonoBehaviour {
    private float _dmg;
    private float _lifetime;
    private float _elapsed;
    private float _dmgAccum;
    private SpriteRenderer _sr;

    private readonly HashSet<EnemyEntity> _tracked  = new HashSet<EnemyEntity>();
    private readonly List<EnemyEntity>    _entering = new List<EnemyEntity>();
    private readonly List<EnemyEntity>    _leaving  = new List<EnemyEntity>();

    private const float FadeTime     = 0.5f;
    private const float CircleRadius = 2.5f;
    private const float DmgInterval  = 3f;
    private static readonly Color TintFull = new Color(0.55f, 0.1f, 1f, 0.75f);

    public static void Spawn(Vector3 pos, float dmg, float lifetime, Sprite spr) {
        var go = new GameObject("BindingCircle");
        go.transform.position   = pos;
        // Pre-set final scale so the circle is the right size immediately.
        go.transform.localScale = Vector3.one * (CircleRadius * 2f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.color        = new Color(TintFull.r, TintFull.g, TintFull.b, 0f); // start invisible
        sr.sortingOrder = 2; // below enemies and most fx
        if (spr != null) sr.sprite = spr;

        var logic      = go.AddComponent<BindingCircleLogic>();
        logic._dmg      = dmg;
        logic._lifetime = lifetime;
        logic._sr       = sr;

        Destroy(go, lifetime + 0.1f);
    }

    void Update() {
        _elapsed += Time.deltaTime;

        // ── Fade alpha ───────────────────────────────────────────────────────
        float fadeInEnd  = FadeTime;
        float fadeOutStart = _lifetime - FadeTime;
        float alpha;
        if (_elapsed < fadeInEnd)
            alpha = _elapsed / FadeTime;
        else if (_elapsed > fadeOutStart)
            alpha = Mathf.Clamp01(1f - (_elapsed - fadeOutStart) / FadeTime);
        else
            alpha = 1f;

        if (_sr != null)
            _sr.color = new Color(TintFull.r, TintFull.g, TintFull.b, TintFull.a * alpha);

        // ── Track enemies entering / leaving ────────────────────────────────
        _entering.Clear();
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, CircleRadius)) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead) continue;
            _entering.Add(e);
        }

        foreach (var e in _entering)
            if (_tracked.Add(e)) e.rootedStacks++;

        _leaving.Clear();
        foreach (var e in _tracked)
            if (!_entering.Contains(e)) _leaving.Add(e);
        foreach (var e in _leaving) {
            e.rootedStacks = Mathf.Max(0, e.rootedStacks - 1);
            _tracked.Remove(e);
        }

        // ── Damage tick (every 3 s) ──────────────────────────────────────────
        _dmgAccum += Time.deltaTime;
        if (_dmgAccum >= DmgInterval) {
            _dmgAccum -= DmgInterval;
            foreach (var e in _tracked)
                if (!e.isDead) e.TakeDamage(_dmg);
        }

        if (_elapsed >= _lifetime) Destroy(gameObject);
    }

    void OnDestroy() {
        foreach (var e in _tracked)
            if (e != null) e.rootedStacks = Mathf.Max(0, e.rootedStacks - 1);
    }
}
