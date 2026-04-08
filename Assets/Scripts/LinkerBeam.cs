using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to a Linker enemy. Finds the nearest other Linker and draws a
/// LineRenderer "beam" between them. Any entity (player or enemy friendly-fire)
/// that crosses within beamWidth of the beam segment takes damage over time.
///
/// Requires: LineRenderer on the same GameObject (auto-added if missing).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LinkerBeam : MonoBehaviour {
    [Header("Beam Settings")]
    public float damagePerSecond = 8f;
    public float beamWidth       = 0.4f;
    public float damageInterval  = 0.3f;

    private LineRenderer _lr;
    private EnemyEntity _self;
    private EnemyEntity _partner;
    private float _dmgTimer;

    void Awake() {
        _lr      = GetComponent<LineRenderer>();
        _self    = GetComponent<EnemyEntity>();

        _lr.startWidth  = beamWidth;
        _lr.endWidth    = beamWidth;
        _lr.positionCount = 2;
        _lr.sortingOrder  = 10;

        // Glowing energy look using the default Unity sprite material
        _lr.material = new Material(Shader.Find("Sprites/Default"));
        _lr.startColor = new Color(0.2f, 0.8f, 1f, 0.85f);
        _lr.endColor   = new Color(0.2f, 0.8f, 1f, 0.85f);
    }

    void Update() {
        if (_self == null || _self.isDead) { _lr.enabled = false; return; }

        // Refresh partner reference periodically
        _partner = FindNearestLinkerPartner();
        if (_partner == null) { _lr.enabled = false; return; }

        _lr.enabled = true;
        Vector3 a = transform.position;
        Vector3 b = _partner.transform.position;
        _lr.SetPosition(0, a);
        _lr.SetPosition(1, b);

        // Damage check
        _dmgTimer -= Time.deltaTime;
        if (_dmgTimer <= 0f) {
            _dmgTimer = damageInterval;
            CheckBeamHits(a, b);
        }
    }

    EnemyEntity FindNearestLinkerPartner() {
        float best = float.MaxValue;
        EnemyEntity found = null;
        foreach (var e in SurvivorMasterScript.Instance.Grid.GetNearby(transform.position)) {
            if (e == null || e.isDead || e == _self) continue;
            if (e.behavior != EnemyBehavior.Linker) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < best) { best = d; found = e; }
        }
        return found;
    }

    void CheckBeamHits(Vector3 a, Vector3 b) {
        // Check player
        Vector3 pPos = SurvivorMasterScript.Instance.player.position;
        if (DistPointToSegment(pPos, a, b) <= beamWidth) {
            SurvivorMasterScript.Instance.TakeDamage(damagePerSecond * damageInterval);
        }

        // Optionally: no friendly-fire on other enemies (comment out block below to enable)
        /*
        foreach (var e in SurvivorMasterScript.Instance.Grid.GetNearby(a)) {
            if (e == null || e.isDead || e == _self || e == _partner) continue;
            if (DistPointToSegment(e.transform.position, a, b) <= beamWidth)
                e.TakeDamage(damagePerSecond * damageInterval * 0.5f);
        }
        */
    }

    /// Returns the shortest distance from point p to line segment (a,b).
    static float DistPointToSegment(Vector3 p, Vector3 a, Vector3 b) {
        Vector3 ab = b - a, ap = p - a;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab));
        return Vector3.Distance(p, a + t * ab);
    }
}
