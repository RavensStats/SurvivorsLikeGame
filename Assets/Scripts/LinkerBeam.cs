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
    public float maxBeamRange    = 17.25f;  // 15 % larger than original; movement caps at 95 %

    // All active LinkerBeam instances register here so partner lookup never
    // relies on SpatialGrid.GetNearby (whose 3x3 cell scan can miss partners
    // that are within maxBeamRange but 2 cells away due to cell-boundary position).
    private static readonly HashSet<LinkerBeam> _registry = new HashSet<LinkerBeam>();

    // Exclusive 1-to-1 pairing table: _pairs[A] == B and _pairs[B] == A.
    // A linker that already appears as a value here is "claimed" and will be
    // skipped by all other linkers during partner search.
    private static readonly Dictionary<EnemyEntity, EnemyEntity> _pairs =
        new Dictionary<EnemyEntity, EnemyEntity>();

    private LineRenderer _lr;
    private EnemyEntity _self;
    private EnemyEntity _partner;   // cached – only re-searched when partner dies

    void Awake() {
        _registry.Add(this);
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

    void OnDestroy() {
        _registry.Remove(this);
        // Free both ends of the pair so the surviving partner can re-pair.
        if (_self != null && _pairs.TryGetValue(_self, out var former)) {
            _pairs.Remove(former);
            _pairs.Remove(_self);
        }
    }

    void Update() {
        if (_self == null || _self.isDead) { _lr.enabled = false; return; }

        if (_self.isCharmed || _self.isPermanentlyCharmed) {
            _lr.enabled = false;
            if (_partner != null) {
                // Release mutual pair so the partner can re-pair with someone else.
                _pairs.Remove(_partner);
                _pairs.Remove(_self);
                _partner = null;
            }
            return;
        }

        // Only re-search when the cached partner is gone – avoids per-frame
        // GetNearby calls that flicker at SpatialGrid cell boundaries.
        if (_partner == null || _partner.isDead)
            _partner = FindNearestLinkerPartner();
        if (_partner == null) { _lr.enabled = false; return; }

        // Only the member with the lower instance ID draws the beam and checks
        // damage.  The partner's LineRenderer is kept off to avoid a double beam.
        bool isOwner = GetInstanceID() < _partner.GetInstanceID();
        _lr.enabled = isOwner;

        if (isOwner) {
            Vector3 a = transform.position;
            Vector3 b = _partner.transform.position;
            _lr.SetPosition(0, a);
            _lr.SetPosition(1, b);
            CheckBeamHits(a, b);
        }
    }

    EnemyEntity FindNearestLinkerPartner() {
        return FindNearestFrom(transform.position, _self, maxBeamRange);
    }

    /// <summary>
    /// Public registry lookup used by EnemyEntity movement so both systems
    /// share the same partner-finding logic without SpatialGrid cell-boundary gaps.
    /// Enforces exclusive 1-to-1 pairing: linkers already claimed by another are skipped.
    /// </summary>
    public static EnemyEntity FindNearestFrom(Vector3 pos, EnemyEntity self, float maxRange) {
        // Return existing live pair immediately – no re-search needed.
        if (self != null && _pairs.TryGetValue(self, out var already)
                && already != null && !already.isDead)
            return already;

        // Release stale pair entry for self if the former partner is dead.
        if (self != null) _pairs.Remove(self);

        float best = float.MaxValue;
        EnemyEntity found = null;
        foreach (var beam in _registry) {
            if (beam._self == null || beam._self.isDead || beam._self == self) continue;
            if (beam._self.isCharmed || beam._self.isPermanentlyCharmed) continue;
            // Skip linkers already exclusively paired with someone other than self.
            if (_pairs.TryGetValue(beam._self, out var theirPair)
                    && theirPair != null && !theirPair.isDead && theirPair != self)
                continue;
            float d = Vector3.Distance(pos, beam.transform.position);
            if (d < best && d <= maxRange) { best = d; found = beam._self; }
        }

        if (found != null && self != null) {
            // Release found's stale pair entry if it had one pointing elsewhere.
            if (_pairs.TryGetValue(found, out var foundOld) && foundOld != self) {
                _pairs.Remove(foundOld); // free the third-party that was paired to found
            }
            // Register mutual exclusive pair.
            _pairs[self]  = found;
            _pairs[found] = self;
        }
        return found;
    }

    void CheckBeamHits(Vector3 a, Vector3 b) {
        // Check player every frame using Time.deltaTime so fast traversals are never missed.
        Vector3 pPos = SurvivorMasterScript.Instance.player.position;
        if (DistPointToSegment(pPos, a, b) <= beamWidth) {
            SurvivorMasterScript.Instance.TakeDamage(damagePerSecond * Time.deltaTime);
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
