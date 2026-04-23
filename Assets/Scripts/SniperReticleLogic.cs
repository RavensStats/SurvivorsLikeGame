using UnityEngine;

// Persistent weapon logic for the Sniper's Sniper Rifle.
// Maintains two visible lime-coloured ring boundaries around the player.
// The reticle sprite wanders slowly inside the annular zone; when an enemy
// enters the zone the reticle locks on rapidly and fires after 1 second.
// One attack is allowed every 3 seconds regardless of level.
//
// Crit scaling:
//   L2 – 10 % crit chance  |  2× crit damage
//   L3 – 25 % crit chance  |  2× crit damage
//   L4 – 50 % crit chance  |  2× crit damage
//   L5 – 50 % crit chance  |  4× crit damage
public class SniperReticleLogic : MonoBehaviour {
    public ItemData weaponData;

    // ── Zone geometry ────────────────────────────────────────────────────────
    private const float InnerRadius = 7f;
    private const float OuterRadius = 12f;

    // ── Timing ───────────────────────────────────────────────────────────────
    private const float AimDuration    = 1f;
    private const float AttackCooldown = 3f;
    private const float WanderSpeed    = 3f;
    private const float LockSpeed      = 22f;
    private const float WanderInterval = 2.5f;

    // ── State ────────────────────────────────────────────────────────────────
    private enum State { Wandering, Aiming, Cooldown }
    private State     _state = State.Wandering;
    private EnemyEntity _target;
    private float     _aimTimer;
    private float     _cooldownTimer;
    private float     _wanderTimer;
    private Vector3   _reticleWorld;
    private Vector3   _wanderDest;

    // ── Child objects ────────────────────────────────────────────────────────
    private GameObject _spriteObj;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    void Start() {
        BuildCircle(InnerRadius);
        BuildCircle(OuterRadius);

        _spriteObj = new GameObject("SniperReticleSprite");
        var sr = _spriteObj.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 9;
        Sprite[] frames = LoadFrames();
        if (frames != null && frames.Length > 0) {
            sr.sprite = frames[0];
            if (frames.Length > 1) { var a = _spriteObj.AddComponent<WeaponSpriteAnimator>(); a.Init(frames); }
        }
        _spriteObj.transform.localScale = Vector3.one * 5f;

        _reticleWorld = SafeRandomZonePos();
        _wanderDest   = SafeRandomZonePos();
    }

    void Update() {
        var sms = SurvivorMasterScript.Instance;
        if (sms?.player == null) return;

        // Root follows player so child circles track without extra code.
        transform.position = sms.player.position;

        switch (_state) {
            case State.Cooldown:
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer <= 0f) _state = State.Wandering;
                Wander(sms.player.position);
                break;

            case State.Wandering:
                Wander(sms.player.position);
                var enemy = FindEnemyInZone(sms.player.position, sms);
                if (enemy != null) {
                    _target   = enemy;
                    _aimTimer = 0f;
                    _state    = State.Aiming;
                }
                break;

            case State.Aiming:
                if (_target == null || _target.isDead || !InZone(_target.transform.position, sms.player.position)) {
                    _target = null;
                    _state  = State.Wandering;
                    break;
                }
                // Rapidly move reticle toward target.
                _reticleWorld = Vector3.MoveTowards(
                    _reticleWorld, _target.transform.position, LockSpeed * Time.deltaTime);

                _aimTimer += Time.deltaTime;
                if (_aimTimer >= AimDuration) {
                    DoFire(sms);
                    _target        = null;
                    _cooldownTimer = AttackCooldown;
                    _state         = State.Cooldown;
                }
                break;
        }

        if (_spriteObj != null)
            _spriteObj.transform.position = _reticleWorld;
    }

    void OnDestroy() {
        if (_spriteObj != null) Destroy(_spriteObj);
    }

    // ── Internal helpers ─────────────────────────────────────────────────────
    void Wander(Vector3 playerPos) {
        _reticleWorld = Vector3.MoveTowards(_reticleWorld, _wanderDest, WanderSpeed * Time.deltaTime);
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f || Vector3.Distance(_reticleWorld, _wanderDest) < 0.15f) {
            _wanderDest  = SafeRandomZonePos();
            _wanderTimer = WanderInterval;
        }
        // Hard-clamp so the reticle never escapes the zone while wandering.
        _reticleWorld = ClampToZone(_reticleWorld, playerPos);
    }

    void DoFire(SurvivorMasterScript sms) {
        if (_target == null || _target.isDead || weaponData == null) return;

        float dmg = weaponData.baseDamage * Mathf.Pow(1.25f, weaponData.level - 1)
                  * (sms?.poiDamageMult ?? 1f)
                  * (1f + RunUpgrades.DamageBonus);

        float critChance = weaponData.level switch {
            2 => 0.10f,
            3 => 0.25f,
            4 => 0.50f,
            _ => weaponData.level >= 5 ? 0.65f : 0f
        };
        float critMult = weaponData.level >= 5 ? 4f : 2f;

        if (critChance > 0f && Random.value < critChance)
            dmg *= critMult;

        _target.TakeDamage(dmg);
    }

    bool InZone(Vector3 worldPos, Vector3 playerPos) {
        float d = Vector2.Distance(worldPos, playerPos);
        return d >= InnerRadius && d <= OuterRadius;
    }

    EnemyEntity FindEnemyInZone(Vector3 playerPos, SurvivorMasterScript sms) {
        var candidates = sms.Grid.GetNearby(playerPos);
        EnemyEntity best = null;
        float bestDist = float.MaxValue;
        foreach (var e in candidates) {
            if (e == null || e.isDead) continue;
            float d = Vector2.Distance(e.transform.position, playerPos);
            if (d >= InnerRadius && d <= OuterRadius && d < bestDist) {
                best = e; bestDist = d;
            }
        }
        return best;
    }

    Vector3 SafeRandomZonePos() {
        var sms = SurvivorMasterScript.Instance;
        Vector3 origin = sms?.player?.position ?? Vector3.zero;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist  = Random.Range(InnerRadius, OuterRadius);
        return origin + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
    }

    Vector3 ClampToZone(Vector3 worldPos, Vector3 playerPos) {
        Vector3 delta = worldPos - playerPos;
        float dist = delta.magnitude;
        if (dist < 0.001f) return playerPos + new Vector3(InnerRadius, 0f, 0f);
        if (dist < InnerRadius) return playerPos + delta.normalized * InnerRadius;
        if (dist > OuterRadius) return playerPos + delta.normalized * OuterRadius;
        return worldPos;
    }

    void BuildCircle(float radius) {
        var go = new GameObject($"SniperRing_{radius}");
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace    = false;
        lr.loop             = true;
        lr.startWidth       = 0.06f;
        lr.endWidth         = 0.06f;
        lr.startColor       = Color.green;   // #00FF00 — lime
        lr.endColor         = Color.green;
        lr.sortingLayerName = "Default";
        lr.sortingOrder     = 2;
        lr.material         = new Material(Shader.Find("Sprites/Default"));

        const int Segments = 64;
        lr.positionCount = Segments;
        float step = 2f * Mathf.PI / Segments;
        for (int i = 0; i < Segments; i++) {
            float a = i * step;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }

    static Sprite[] LoadFrames() {
        var frames = Resources.LoadAll<Sprite>("Sprites/Weapons/SniperReticle");
        if (frames != null && frames.Length > 1)
            System.Array.Sort(frames, (a, b) =>
                string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return frames;
    }
}
