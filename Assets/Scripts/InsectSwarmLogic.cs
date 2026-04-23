using System.Collections.Generic;
using UnityEngine;

// Individual insect unit spawned by the Hivemaster's Insect Swarm weapon.
// Moves toward the nearest enemy using the spatial grid, plays directional
// sprite animation identical to enemy animations (8-direction, folder-per-direction
// under Resources/Sprites/Weapons/{spriteFolder}/{dirName}/), and attacks adjacent
// enemies every 2 seconds.
//
// Has 1 HP — one hit from any source (enemy bullet, melee, AOE) destroys it.
// Registers in Active so EnemyAttack can iterate and damage it.
public class InsectSwarmLogic : MonoBehaviour {
    public static readonly List<InsectSwarmLogic> Active = new List<InsectSwarmLogic>();

    public float hp     = 1f;
    public bool isDead  = false;

    private float _dmg;
    private float _attackTimer;

    private const float MoveSpeed      = 4f;
    private const float AttackRange    = 1.5f;
    private const float AttackInterval = 2f;
    private const float AnimFps        = 10f;

    // ── Directional animation ─────────────────────────────────────────────────
    // Mirrors EnemyAnimator: index 0=S,1=SW,2=W,3=NW,4=N,5=NE,6=E,7=SE
    private static readonly string[] DirNames = {
        "south", "south-west", "west", "north-west",
        "north", "north-east", "east", "south-east"
    };
    private Sprite[][]   _dirFrames;
    private bool         _hasSprites;
    private SpriteRenderer _sr;
    private int   _currentDir;
    private int   _currentFrame;
    private float _frameTimer;

    // ── Factory ───────────────────────────────────────────────────────────────
    public static InsectSwarmLogic Spawn(Vector3 pos, float dmg, string spriteFolder) {
        var go = new GameObject("InsectSwarm");
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 5;
        go.transform.localScale = Vector3.one * 3f;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var logic      = go.AddComponent<InsectSwarmLogic>();
        logic._dmg     = dmg;
        logic._sr      = sr;
        logic.LoadDirectionalSprites(spriteFolder);

        Destroy(go, 60f); // safety lifetime — swarms die on first hit in practice
        return logic;
    }

    void Awake()     => Active.Add(this);
    void OnDestroy() => Active.Remove(this);

    // ── Per-frame logic ───────────────────────────────────────────────────────
    void Update() {
        if (isDead) return;

        var sms = SurvivorMasterScript.Instance;
        if (sms == null) return;

        EnemyEntity target = FindNearest(sms);
        Vector2 moveDir = Vector2.zero;

        if (target != null) {
            float dist = Vector2.Distance(transform.position, target.transform.position);
            moveDir = ((Vector2)(target.transform.position - transform.position)).normalized;

            if (dist > AttackRange)
                transform.position += (Vector3)(moveDir * MoveSpeed * Time.deltaTime);

            _attackTimer -= Time.deltaTime;
            if (dist <= AttackRange && _attackTimer <= 0f) {
                _attackTimer = AttackInterval;
                target.TakeDamage(_dmg);
            }
        }

        AdvanceAnimation(moveDir);
    }

    // ── Public damage interface (used by EnemyAttack and EnemyBullet) ─────────
    public void TakeDamage(float d) {
        if (isDead) return;
        hp -= d;
        if (hp <= 0f) Die();
    }

    void Die() {
        isDead = true;
        Destroy(gameObject);
    }

    // ── Nearest-enemy search ──────────────────────────────────────────────────
    EnemyEntity FindNearest(SurvivorMasterScript sms) {
        var candidates = sms.Grid.GetNearby(transform.position);
        EnemyEntity best = null;
        float bestSq = float.MaxValue;
        foreach (var e in candidates) {
            if (e == null || e.isDead) continue;
            float sq = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    // ── Directional animation ─────────────────────────────────────────────────
    void LoadDirectionalSprites(string spriteFolder) {
        _dirFrames = new Sprite[DirNames.Length][];
        bool anyLoaded = false;
        for (int d = 0; d < DirNames.Length; d++) {
            string path = $"Sprites/Weapons/{spriteFolder}/{DirNames[d]}";
            Sprite[] frames = Resources.LoadAll<Sprite>(path);
            if (frames != null && frames.Length > 0) {
                System.Array.Sort(frames, (a, b) =>
                    string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                anyLoaded = true;
            }
            _dirFrames[d] = frames ?? new Sprite[0];
        }
        _hasSprites = anyLoaded;

        // Show first frame immediately so the sprite isn't blank on frame 0.
        if (anyLoaded) {
            Sprite[] south = _dirFrames[0];
            if (south != null && south.Length > 0) _sr.sprite = south[0];
        }
    }

    void AdvanceAnimation(Vector2 moveDir) {
        if (!_hasSprites) return;

        if (moveDir.sqrMagnitude > 0.01f)
            _currentDir = VelocityToDir(moveDir);

        _frameTimer += Time.deltaTime;
        if (_frameTimer < 1f / AnimFps) return;
        _frameTimer -= 1f / AnimFps;

        Sprite[] frames = _dirFrames[_currentDir];
        if (frames == null || frames.Length == 0) frames = _dirFrames[0]; // south fallback
        if (frames == null || frames.Length == 0) return;

        _currentFrame = (_currentFrame + 1) % frames.Length;
        _sr.sprite = frames[_currentFrame];
    }

    // Identical formula to EnemyAnimator.VelocityToDir.
    static int VelocityToDir(Vector2 vel) {
        float angle   = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        float compass = (-(angle - 90f) + 360f) % 360f;
        float shifted = (compass + 180f) % 360f;
        return Mathf.RoundToInt(shifted / 45f) % 8;
    }
}
