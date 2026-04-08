using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEntity : MonoBehaviour {
    public EnemyBehavior behavior;
    public float hp = 50, stun;
    public float moveSpeed  = 3.5f;
    public float attackRange = 1.0f;
    public bool  isDead = false;
    public Vector2Int currentCell = new Vector2Int(-99, -99);

    // ── Behavior timers / state ────────────────────────────────────────────────
    private float chargeTimer    = 0f;
    private float shamHealTimer  = 0f;
    private bool  isCharging     = false;
    private Vector3 chargeDir;

    // Tank


    // Ghost / invisibility
    private float _ghostCycleTimer = 0f;
    private bool  _ghostInvisible  = false;
    private SpriteRenderer _sr;

    // Freezer – zigzag + ice trail
    private float _freezerZigTimer = 0f;
    private int   _freezerZigDir   = 1;

    // Assassin – teleport strike
    private float _assassinTimer = 0f;

    // Necromancer enemy – summon cooldown
    private float _necroSummonTimer = 0f;

    // Thief – stole once
    private bool  _thiefStole = false;

    // Mimic – dormant until player approaches
    private bool   _mimicAwake    = false;
    private Sprite _mimicGemSprite;

    // Sniper – aim delay
    private float   _sniperTimer    = 0f;
    private bool    _sniperAiming   = false;
    private Vector3 _sniperAimDir;

    // Vampire enemy – ult drain  
    private float _vampireTimer = 0f;

    // Chrono – tick-teleport + cooldown freeze
    private float _chronoTickTimer  = 0f;
    private float _chronoFreezeTimer = 0f;

    // Burrower – underground phase
    private bool  _burrowed      = false;
    private float _burrowTimer   = 0f;


    // Parasite – slow aura while latched
    private bool  _latched     = false;
    private float _latchTimer  = 0f;

    // Puffer – inflate state
    private float _pufferBaseScale;
    private float _pufferBaseDamage;
    private bool  _pufferInflated = false;

    // Jockey – control reversal
    private bool  _jockeyLatched = false;
    private float _jockeyTimer   = 0f;

    // Mortar – retreat + fire
    private float _mortarTimer = 0f;

    // Siren – weapon disable
    private float _sirenTimer = 0f;

    // Webber – rectangular path + web trail
    private float   _webberPhaseTimer = 0f;
    private int     _webberPhase      = 0;
    private float   _webTrailTimer    = 0f;

    // Commander – speed-aura radius
    private float _commanderAuraTimer = 0f;

    // Reflector – shield active


    // ── Visuals ────────────────────────────────────────────────────────────────
    public EnemyTier tier = EnemyTier.Normal;
    private float      _maxHp;
    private GameObject _hpBarRoot;
    private Transform  _hpBarFill;
    private float      _hpVisTimer;
    private static Sprite _pixelSprite;

    void Start() {
        _sr = GetComponent<SpriteRenderer>();
        if (SurvivorMasterScript.Instance.nemesis.isPendingRevenge &&
            behavior == SurvivorMasterScript.Instance.nemesis.killerType) {
            transform.localScale *= 2; hp *= 5; // Nemesis Buff
        }
        _maxHp = hp;
        _pufferBaseScale  = transform.localScale.x;
        _pufferBaseDamage = GetComponent<EnemyAttack>() != null ? GetComponent<EnemyAttack>().damage : 10f;
        // Mimics impersonate an XP gem until the player is close
        if (behavior == EnemyBehavior.Mimic) {
            _mimicGemSprite = Resources.Load<Sprite>("Sprites/Gems/gem");
            if (_sr != null && _mimicGemSprite != null) _sr.sprite = _mimicGemSprite;
        }
        CreateHealthBar();
    }

    void Update() {
        if (isDead) return;
        if (stun > 0) { stun -= Time.deltaTime; return; }
        SurvivorMasterScript.Instance.Grid.UpdateEntity(this, transform.position);

        Vector3 pPos = SurvivorMasterScript.Instance.player.position;
        Vector3 dir  = (pPos - transform.position).normalized;
        float   dist = Vector3.Distance(transform.position, pPos);
        float   s    = moveSpeed * (SurvivorMasterScript.Instance.isInsideGraveyard ? 1.57f : 1f);

        // ── Jockey control-reversal (inverts input via PlayerMovement hack) ──
        if (_jockeyLatched) {
            _jockeyTimer -= Time.deltaTime;
            if (_jockeyTimer <= 0f) {
                _jockeyLatched = false;
                var pm = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
                if (pm != null) pm.inputScale = 1f;
                // reposition self next to player
                transform.position = pPos + (Vector3)Random.insideUnitCircle.normalized * 2f;
            }
            return; // enemy stays riding
        }

        // ── Chrono cooldown-freeze active ──────────────────────────────────
        if (_chronoFreezeTimer > 0f) {
            _chronoFreezeTimer -= Time.deltaTime;
            WeaponSystem.Instance?.SetFrozen(_chronoFreezeTimer > 0f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BEHAVIOR BRANCH
        // ─────────────────────────────────────────────────────────────────────
        switch (behavior) {

            // ── Already implemented (kept + extended) ────────────────────────
            case EnemyBehavior.Glitch:
                if (Time.time % 3 < 0.02f) transform.position = pPos + (Vector3)Random.insideUnitCircle * 5f;
                break;

            case EnemyBehavior.Magnet:
            case EnemyBehavior.BlackHole:
                if (dist < 20f)
                    SurvivorMasterScript.Instance.player.position =
                        Vector3.MoveTowards(pPos, transform.position, 0.5f * Time.deltaTime);
                break;

            case EnemyBehavior.Ranged:
                if (dist < 6f)       transform.position += -dir * s * Time.deltaTime;
                else if (dist > 12f) transform.position += dir * (s * 0.5f) * Time.deltaTime;
                else {
                    Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                    transform.position += perp * s * Time.deltaTime;
                }
                break;

            case EnemyBehavior.Charger:
                chargeTimer -= Time.deltaTime;
                if (isCharging) {
                    transform.position += chargeDir * s * 3.5f * Time.deltaTime;
                    if (chargeTimer <= 0f) { isCharging = false; chargeTimer = 3f; }
                } else {
                    if (dist > attackRange) transform.position += dir * s * Time.deltaTime;
                    if (chargeTimer <= 0f) { isCharging = true; chargeDir = dir; chargeTimer = 0.4f; }
                }
                break;

            case EnemyBehavior.Exploder:
                transform.position += dir * s * 1.4f * Time.deltaTime;
                if (dist < 1.5f) {
                    SurvivorMasterScript.Instance.TakeDamage(hp * 0.5f);
                    TakeDamage(9999f);
                }
                break;

            case EnemyBehavior.Shaman:
                if (dist > attackRange) transform.position += dir * s * 0.6f * Time.deltaTime;
                shamHealTimer -= Time.deltaTime;
                if (shamHealTimer <= 0f) {
                    shamHealTimer = 5f;
                    foreach (var e in SurvivorMasterScript.Instance.Grid.GetNearby(transform.position))
                        if (e != null && !e.isDead && e != this) e.hp += 10f;
                }
                break;

            // ── Tank – very slow, massive contact damage, no knockback ────────
            case EnemyBehavior.Tank:
                transform.position += dir * (s * 0.35f) * Time.deltaTime;
                if (dist < attackRange) SurvivorMasterScript.Instance.TakeDamage(8f * Time.deltaTime);
                break;

            // ── Ghost – periodic invisibility / untargetable ──────────────────
            case EnemyBehavior.Ghost:
                transform.position += dir * s * Time.deltaTime;
                _ghostCycleTimer += Time.deltaTime;
                if (!_ghostInvisible && _ghostCycleTimer > 2.5f) {
                    _ghostInvisible = true; _ghostCycleTimer = 0f;
                    if (_sr) _sr.color = new Color(1, 1, 1, 0.15f);
                    isDead = false; // set untargetable flag via alpha — projectiles skip 0-alpha check
                } else if (_ghostInvisible && _ghostCycleTimer > 1.5f) {
                    _ghostInvisible = false; _ghostCycleTimer = 0f;
                    if (_sr) _sr.color = Color.white;
                }
                break;

            // ── Freezer – zigzag + ice trail slowing player ───────────────────
            case EnemyBehavior.Freezer:
                _freezerZigTimer += Time.deltaTime;
                if (_freezerZigTimer > 0.6f) { _freezerZigTimer = 0f; _freezerZigDir *= -1; }
                Vector3 zigPerp = new Vector3(-dir.y, dir.x, 0f) * _freezerZigDir;
                transform.position += (dir + zigPerp * 0.7f).normalized * s * Time.deltaTime;
                // Slow trail: check if player is near this position
                if (Vector3.Distance(transform.position, pPos) < 1.5f) {
                    var pm2 = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
                    if (pm2 != null) StartCoroutine(TempSlow(pm2, 0.5f, 2f));
                }
                break;

            // ── Swarmer – fast, low damage, chases directly ───────────────────
            case EnemyBehavior.Swarmer:
                transform.position += dir * (s * 1.6f) * Time.deltaTime;
                if (dist < attackRange) SurvivorMasterScript.Instance.TakeDamage(3f * Time.deltaTime);
                break;

            // ── ShieldBearer – only moves when player faces away ──────────────
            case EnemyBehavior.ShieldBearer: {
                Vector2 playerFacing = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>()?.LastFacing ?? Vector2.down;
                Vector2 toEnemy      = ((Vector2)(transform.position - pPos)).normalized;
                float   faceDot      = Vector2.Dot(playerFacing, toEnemy);
                if (faceDot < 0f) // player not looking at us
                    transform.position += dir * s * Time.deltaTime;
                break;
            }

            // ── Assassin enemy – teleport then strike ─────────────────────────
            case EnemyBehavior.Assassin:
                _assassinTimer -= Time.deltaTime;
                if (_assassinTimer <= 0f) {
                    _assassinTimer = Random.Range(2f, 4f);
                    // Teleport to just outside melee range
                    transform.position = pPos + (Vector3)(Random.insideUnitCircle.normalized * 1.8f);
                    SurvivorMasterScript.Instance.TakeDamage(15f);
                }
                break;

            // ── Necromancer enemy – summon escalating minions ─────────────────
            case EnemyBehavior.Necromancer: {
                // Stay near screen edge (~25 units away)
                if (dist < 22f) transform.position -= dir * s * 0.5f * Time.deltaTime;
                _necroSummonTimer -= Time.deltaTime;
                if (_necroSummonTimer <= 0f) {
                    _necroSummonTimer = 10f;
                    float mins = SurvivorMasterScript.Instance.GameTime / 60f;
                    EnemyBehavior[] pool = mins < 3f
                        ? new[] { EnemyBehavior.Chaser }
                        : mins < 7f
                        ? new[] { EnemyBehavior.Chaser, EnemyBehavior.Swarmer }
                        : new[] { EnemyBehavior.Chaser, EnemyBehavior.Swarmer, EnemyBehavior.Charger };
                    float summonHP  = 20f + mins * 3f;
                    for (int i = 0; i < 3; i++) {
                        EnemyBehavior bt = pool[Random.Range(0, pool.Length)];
                        SpawnMinion(bt, pPos + (Vector3)(Random.insideUnitCircle * 3f), summonHP);
                    }
                }
                break;
            }

            // ── Thief – fast dash past player, steal a gold coin ──────────────
            case EnemyBehavior.Thief:
                transform.position += dir * (s * 2f) * Time.deltaTime;
                if (!_thiefStole && dist < 1f) {
                    _thiefStole = true;
                    if (SurvivorMasterScript.GlobalGold > 0) SurvivorMasterScript.GlobalGold--;
                    // Run away after stealing
                    moveSpeed *= -1f;
                }
                if (moveSpeed < 0f) transform.position -= dir * Mathf.Abs(s) * 2f * Time.deltaTime;
                break;

            // ── Commander – buff nearby enemy speed ───────────────────────────
            case EnemyBehavior.Commander:
                // Hang back 8 units
                if (dist < 8f)       transform.position -= dir * s * 0.5f * Time.deltaTime;
                else if (dist > 14f) transform.position += dir * s * 0.4f * Time.deltaTime;
                _commanderAuraTimer -= Time.deltaTime;
                if (_commanderAuraTimer <= 0f) {
                    _commanderAuraTimer = 3f;
                    foreach (var e in SurvivorMasterScript.Instance.Grid.GetNearby(transform.position))
                        if (e != null && !e.isDead && e != this) e.moveSpeed = Mathf.Min(e.moveSpeed * 1.1f, 15f);
                }
                break;

            // ── Mimic – dormant gem until player close, then lunge ────────────
            case EnemyBehavior.Mimic:
                if (!_mimicAwake) {
                    if (dist < 3f) {
                        _mimicAwake = true;
                        if (_sr) _sr.color = Color.red;   // flash to signal reveal
                    }
                } else {
                    transform.position += dir * (s * 2.5f) * Time.deltaTime;
                    if (dist < attackRange) SurvivorMasterScript.Instance.TakeDamage(12f * Time.deltaTime);
                }
                break;

            // ── Sniper enemy – aim lock then fire hitscan ─────────────────────
            case EnemyBehavior.SniperElite:
            case EnemyBehavior.Sniper:
                // Stay stationary at range
                if (dist < 20f) transform.position -= dir * s * 0.3f * Time.deltaTime;
                _sniperTimer -= Time.deltaTime;
                if (!_sniperAiming && _sniperTimer <= 0f) {
                    _sniperAiming   = true;
                    _sniperAimDir   = dir;
                    _sniperTimer    = 1.0f; // 1s aim window
                    if (_sr) _sr.color = new Color(1f, 0.3f, 0.3f); // telegraphing tint
                } else if (_sniperAiming && _sniperTimer <= 0f) {
                    _sniperAiming = false; _sniperTimer = Random.Range(3f, 5f);
                    if (_sr) _sr.color = Color.white;
                    // Hit if player still in the aim direction (±15°)
                    Vector3 curDir = (pPos - transform.position).normalized;
                    if (Vector3.Angle(_sniperAimDir, curDir) < 15f)
                        SurvivorMasterScript.Instance.TakeDamage(25f);
                }
                break;

            // ── Vampire enemy – erratic flutter, drain ult meter ─────────────
            case EnemyBehavior.VampireBat:
            case EnemyBehavior.Vampire: {
                Vector3 flutter = new Vector3(Mathf.Sin(Time.time * 7f), Mathf.Cos(Time.time * 5.3f), 0f);
                transform.position += (dir * s + flutter * 1.5f) * Time.deltaTime;
                _vampireTimer -= Time.deltaTime;
                if (_vampireTimer <= 0f && dist < 2f) {
                    _vampireTimer = 1.5f;
                    // Drain ult meter by poking ultTimer down (not below 0)
                    SurvivorMasterScript.Instance.DrainUlt(5f);
                }
                break;
            }

            // ── Chrono – tick-teleport, freeze player cooldowns on hit ────────
            case EnemyBehavior.Chrono:
                _chronoTickTimer -= Time.deltaTime;
                if (_chronoTickTimer <= 0f) {
                    _chronoTickTimer = 0.8f;
                    // Teleport toward player in a step
                    transform.position = Vector3.MoveTowards(transform.position, pPos, 3f);
                    if (dist < 2f) {
                        _chronoFreezeTimer = 3f;
                        WeaponSystem.Instance?.SetFrozen(true);
                    }
                }
                break;

            // ── Reflector – faces player, bounces projectiles ─────────────────
            case EnemyBehavior.Reflector:
                transform.position += dir * (s * 0.4f) * Time.deltaTime;
                // Shield logic handled in ProjectileLogic via tag "Reflector"
                break;

            // ── Burrower – invulnerable underground, surface-strike ───────────
            case EnemyBehavior.Burrower:
                _burrowTimer -= Time.deltaTime;
                if (_burrowed) {
                    if (_sr) _sr.color = new Color(1, 1, 1, 0f); // hidden
                    // Move underground
                    transform.position = Vector3.MoveTowards(transform.position, pPos, s * Time.deltaTime);
                    if (_burrowTimer <= 0f) {
                        _burrowed = false; _burrowTimer = Random.Range(3f, 5f);
                        transform.position = pPos + Vector3.up * 0.5f;
                        if (_sr) _sr.color = Color.white;
                        SurvivorMasterScript.Instance.TakeDamage(18f);
                    }
                } else {
                    if (_burrowTimer <= 0f) { _burrowed = true; _burrowTimer = Random.Range(2f, 3.5f); }
                    else transform.position += dir * (s * 0.5f) * Time.deltaTime;
                }
                break;

            // ── Parasite – latch onto player, slow them ───────────────────────
            case EnemyBehavior.Parasite:
                if (!_latched) {
                    transform.position += dir * (s * 2.5f) * Time.deltaTime;
                    if (dist < 0.8f) {
                        _latched = true; _latchTimer = 4f;
                        var pm3 = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
                        if (pm3 != null) pm3.inputScale = 0.4f;
                    }
                } else {
                    transform.position = pPos; // ride the player
                    _latchTimer -= Time.deltaTime;
                    SurvivorMasterScript.Instance.TakeDamage(2f * Time.deltaTime);
                    if (_latchTimer <= 0f) {
                        _latched = false;
                        var pm3 = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
                        if (pm3 != null) pm3.inputScale = 1f;
                        transform.position = pPos + (Vector3)(Random.insideUnitCircle * 3f);
                    }
                }
                break;

            // ── Puffer – idle wander, inflate when damaged ────────────────────
            case EnemyBehavior.Puffer: {
                Vector3 wander = new Vector3(Mathf.Sin(Time.time * 0.6f + GetInstanceID()), Mathf.Cos(Time.time * 0.7f + GetInstanceID()), 0f);
                transform.position += wander * s * 0.5f * Time.deltaTime;
                break;
            }

            // ── Jockey – flank rear, leap (reverse controls) ─────────────────
            case EnemyBehavior.Jockey: {
                Vector2 pf = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>()?.LastFacing ?? Vector2.down;
                Vector3 behindPlayer = pPos - (Vector3)(pf.normalized * 1.5f);
                transform.position = Vector3.MoveTowards(transform.position, behindPlayer, s * Time.deltaTime);
                if (Vector3.Distance(transform.position, behindPlayer) < 0.5f && !_jockeyLatched) {
                    _jockeyLatched = true; _jockeyTimer = 3f;
                    var pm4 = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
                    if (pm4 != null) pm4.inputScale = -1f;
                }
                break;
            }

            // ── Mortar – flee player, lob AoE shots ──────────────────────────
            case EnemyBehavior.Mortar:
                if (dist < 12f) transform.position -= dir * s * 0.8f * Time.deltaTime;
                _mortarTimer -= Time.deltaTime;
                if (_mortarTimer <= 0f) {
                    _mortarTimer = 3f;
                    SpawnMortarShell(pPos + (Vector3)(Random.insideUnitCircle * 2f));
                }
                break;

            // ── Siren – hover, disable one weapon ────────────────────────────
            case EnemyBehavior.Siren:
                if (dist < 8f)       transform.position -= dir * s * 0.5f * Time.deltaTime;
                else if (dist > 15f) transform.position += dir * s * 0.4f * Time.deltaTime;
                _sirenTimer -= Time.deltaTime;
                if (_sirenTimer <= 0f) {
                    _sirenTimer = 5f;
                    WeaponSystem.Instance?.DisableRandomWeapon(2f);
                }
                break;

            // ── Webber – rectangular patrol, slow web trail ───────────────────
            case EnemyBehavior.Webber: {
                _webberPhaseTimer -= Time.deltaTime;
                Vector3[] phases = { Vector3.right, Vector3.up, Vector3.left, Vector3.down };
                transform.position += phases[_webberPhase % 4] * s * Time.deltaTime;
                if (_webberPhaseTimer <= 0f) { _webberPhase++; _webberPhaseTimer = 1.2f; }
                // Drop web that slows the player temporarily
                _webTrailTimer -= Time.deltaTime;
                if (_webTrailTimer <= 0f) {
                    _webTrailTimer = 0.5f;
                    if (dist < 1.5f) {
                        var pm5 = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
                        if (pm5 != null) StartCoroutine(TempSlow(pm5, 0.5f, 2f));
                    }
                }
                break;
            }

            // ── Default Chaser ─────────────────────────────────────────────────
            default:
                if (dist > attackRange) transform.position += dir * s * Time.deltaTime;
                break;
        }

        // ── Health bar follow + periodic visibility refresh ──────────────────
        if (_hpBarRoot != null) {
            _hpBarRoot.transform.position = transform.position + Vector3.up * 1.8f;
            _hpVisTimer -= Time.deltaTime;
            if (_hpVisTimer <= 0f) {
                _hpVisTimer = 0.5f;
                bool show = ShouldShowHealthBar();
                if (_hpBarRoot.activeSelf != show) _hpBarRoot.SetActive(show);
            }
        }
    }

    // ── Ghost: immune to damage while invisible ──────────────────────────────
    public bool IsInvulnerable() => _ghostInvisible || _burrowed;

    public void Stun(float d) => stun = d;
    public void TakeDamage(float d) {
        if (isDead) return;
        if (IsInvulnerable()) return;
        // Puffer: inflate on hit
        if (behavior == EnemyBehavior.Puffer && !_pufferInflated) {
            _pufferInflated = true;
            transform.localScale *= 1.8f;
            hp *= 1.5f; _maxHp = hp;
            var ea = GetComponent<EnemyAttack>();
            if (ea != null) ea.damage *= 1.5f;
        }
        SurvivorMasterScript.Instance.RegisterDamageDealt(d);
        var b = SurvivorMasterScript.Instance.bestiary.Find(x => x.behavior == behavior);
        if (b != null && b.isHunterBonusUnlocked) d *= 1.15f;
        FloatingText.Spawn(transform.position, d);
        hp -= d;
        UpdateHealthBar();
        if (hp <= 0) Die();
    }

    public void ApplyKnockback(Vector2 knockDir, float force) {
        // Tank ignores knockback
        if (isDead || force <= 0f || behavior == EnemyBehavior.Tank) return;
        StartCoroutine(KnockbackRoutine(knockDir, force));
    }
    IEnumerator KnockbackRoutine(Vector2 knockDir, float force) {
        float duration = 0.12f, elapsed = 0f;
        while (elapsed < duration && !isDead) {
            transform.position += (Vector3)(knockDir * force * 10f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator TempSlow(PlayerMovement pm, float scale, float duration) {
        pm.inputScale = scale;
        yield return new WaitForSeconds(duration);
        if (pm != null) pm.inputScale = 1f;
    }

    void Die() {
        isDead = true;
        SurvivorMasterScript.Instance.RegisterKill(behavior);
        SurvivorMasterScript.Instance.Grid.Remove(this);

        // Tank explodes on death
        if (behavior == EnemyBehavior.Tank) {
            var nearby = SurvivorMasterScript.Instance.Grid.GetNearby(transform.position);
            if (Vector3.Distance(transform.position, SurvivorMasterScript.Instance.player.position) < 4f)
                SurvivorMasterScript.Instance.TakeDamage(30f);
        }
        // Parasite/Jockey: unfreeze player
        if (behavior == EnemyBehavior.Parasite || behavior == EnemyBehavior.Jockey) {
            var pm = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
            if (pm != null) pm.inputScale = 1f;
        }
        // Unfreeze weapon cooldowns
        if (behavior == EnemyBehavior.Chrono) WeaponSystem.Instance?.SetFrozen(false);

        XpGem.Spawn(transform.position);
        if (Random.value < 0.10f) GoldCoin.Spawn(transform.position, Random.Range(1, 3));
        Destroy(gameObject, 0.15f);
    }

    // ── Necromancer summon helper ─────────────────────────────────────────────
    void SpawnMinion(EnemyBehavior bt, Vector3 pos, float minionHP) {
        var go = new GameObject($"Minion_{bt}");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 7f;
        go.tag = "Enemy";
        go.AddComponent<SpriteRenderer>().sortingOrder = 5;
        var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0f;
        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = false; col.radius = 0.08f;
        var e = go.AddComponent<EnemyEntity>(); e.behavior = bt; e.hp = minionHP; e.moveSpeed = 4f;
        var atk = go.AddComponent<EnemyAttack>(); atk.damage = 5f; atk.attackInterval = 1f;
        SurvivorMasterScript.Instance.Grid.UpdateEntity(e, pos);
    }

    // ── Mortar shell helper ───────────────────────────────────────────────────
    void SpawnMortarShell(Vector3 target) {
        StartCoroutine(MortarImpact(target));
    }
    IEnumerator MortarImpact(Vector3 target) {
        yield return new WaitForSeconds(1.2f); // flight time
        if (isDead) yield break;
        // AoE damage at landing point
        if (Vector3.Distance(target, SurvivorMasterScript.Instance.player.position) < 2.5f)
            SurvivorMasterScript.Instance.TakeDamage(14f);
        // Damage enemies caught in splash too (friendly fire for chaos)
        foreach (var e in SurvivorMasterScript.Instance.Grid.GetNearby(target))
            if (e != null && !e.isDead && Vector3.Distance(e.transform.position, target) < 2f)
                e.TakeDamage(6f);
    }

    void OnDestroy() {
        if (_hpBarRoot != null) Destroy(_hpBarRoot);
    }

    // ── Health bar ─────────────────────────────────────────────────────────────
    void CreateHealthBar() {
        const float barW = 2.0f, barH = 0.18f;
        _hpBarRoot = new GameObject("HPBar");

        var bg   = new GameObject("BG");
        bg.transform.SetParent(_hpBarRoot.transform);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = GetPixelSprite();
        bgSr.color  = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        bgSr.sortingOrder = 8;
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale    = new Vector3(barW, barH, 1f);

        var fill   = new GameObject("Fill");
        fill.transform.SetParent(_hpBarRoot.transform);
        var fillSr = fill.AddComponent<SpriteRenderer>();
        fillSr.sprite = GetPixelSprite();
        fillSr.color  = Color.red;
        fillSr.sortingOrder = 9;
        fill.transform.localPosition = Vector3.zero;
        fill.transform.localScale    = new Vector3(barW, barH, 1f);
        _hpBarFill = fill.transform;

        _hpBarRoot.transform.position = transform.position + Vector3.up * 1.8f;
        _hpBarRoot.SetActive(ShouldShowHealthBar());
    }

    void UpdateHealthBar() {
        if (_hpBarFill == null) return;
        const float barW = 2.0f, barH = 0.18f;
        float pct = _maxHp > 0f ? Mathf.Clamp01(hp / _maxHp) : 0f;
        _hpBarFill.localScale    = new Vector3(barW * pct, barH, 1f);
        _hpBarFill.localPosition = new Vector3(barW * (pct - 1f) * 0.5f, 0f, 0f);
    }

    bool ShouldShowHealthBar() {
        switch (tier) {
            case EnemyTier.MiniBoss: return PlayerPrefs.GetInt("showHpMiniBoss", 1) == 1;
            case EnemyTier.Boss:     return PlayerPrefs.GetInt("showHpBoss",     1) == 1;
            default:                 return PlayerPrefs.GetInt("showHpNormal",   0) == 1;
        }
    }

    static Sprite GetPixelSprite() {
        if (_pixelSprite != null) return _pixelSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        return _pixelSprite;
    }
}