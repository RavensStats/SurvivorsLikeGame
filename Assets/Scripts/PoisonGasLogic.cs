using System.Collections.Generic;
using UnityEngine;

// Persistent poison gas cloud for the PlagueDoctor's Plague Canister weapon.
// Expands from zero to full radius over 0.5 s, then lingers for the remainder
// of its 5-second lifetime. Deals damage every second to enemies inside it.
// Any enemy inside the cloud has a 25% chance to miss their attacks (handled
// via EnemyEntity.poisonGasStacks checked in EnemyAttack).
public class PoisonGasLogic : MonoBehaviour {
    private float _dmg;
    private float _maxRadius;
    private float _elapsed;
    private float _dmgAccum;

    // Reusable scratch collections — instance-level to avoid cross-cloud clobbering.
    private readonly HashSet<EnemyEntity> _tracked  = new HashSet<EnemyEntity>();
    private readonly List<EnemyEntity>    _entering = new List<EnemyEntity>();
    private readonly List<EnemyEntity>    _leaving  = new List<EnemyEntity>();

    private SpriteRenderer _sr;

    private const float ExpandTime = 0.5f;
    private const float Lifetime   = 5f;

    public static void Spawn(Vector3 pos, float dmg, float maxRadius, Sprite spr) {
        var go = new GameObject("PoisonGas");
        go.transform.position   = pos;
        go.transform.localScale = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.color        = new Color(0.25f, 0.85f, 0.1f, 0.5f);
        sr.sortingOrder = 3; // render below enemies
        if (spr != null) sr.sprite = spr;

        var logic = go.AddComponent<PoisonGasLogic>();
        logic._dmg       = dmg;
        logic._maxRadius = maxRadius;
        logic._sr        = sr;

        Destroy(go, Lifetime + 0.1f);
    }

    void Update() {
        _elapsed += Time.deltaTime;

        // ── Expand ───────────────────────────────────────────────────────────
        float t             = Mathf.Clamp01(_elapsed / ExpandTime);
        float currentRadius = _maxRadius * t;
        // Scale so the sprite's natural 1-unit diameter fills the cloud diameter.
        transform.localScale = Vector3.one * (currentRadius * 2f);

        // ── Track enemies entering / leaving the cloud ────────────────────
        _entering.Clear();
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, currentRadius)) {
            if (!col.CompareTag("Enemy")) continue;
            var e = col.GetComponent<EnemyEntity>();
            if (e == null || e.isDead) continue;
            _entering.Add(e);
        }

        // New entrants
        foreach (var e in _entering)
            if (_tracked.Add(e)) e.poisonGasStacks++;

        // Leavers
        _leaving.Clear();
        foreach (var e in _tracked)
            if (!_entering.Contains(e)) _leaving.Add(e);
        foreach (var e in _leaving) {
            e.poisonGasStacks = Mathf.Max(0, e.poisonGasStacks - 1);
            _tracked.Remove(e);
        }

        // ── Damage tick (every 1 s) ──────────────────────────────────────
        _dmgAccum += Time.deltaTime;
        if (_dmgAccum >= 1f) {
            _dmgAccum -= 1f;
            foreach (var e in _tracked)
                if (!e.isDead) e.TakeDamage(_dmg);
        }

        if (_elapsed >= Lifetime) Destroy(gameObject);
    }

    void OnDestroy() {
        // Release stacks for any enemies still inside when the cloud despawns.
        foreach (var e in _tracked)
            if (e != null) e.poisonGasStacks = Mathf.Max(0, e.poisonGasStacks - 1);
    }
}
