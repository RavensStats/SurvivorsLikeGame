using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEntity : MonoBehaviour {
    public EnemyBehavior behavior;
    public float hp = 50, stun;
    public float moveSpeed  = 3.5f;
    public float attackRange = 1.0f;
    public bool  isDead = false;
    // Incremented when inside a PoisonGas cloud, decremented on exit. >0 means 25% attack miss.
    public int poisonGasStacks = 0;
    // Incremented when inside a BindingCircle, decremented on exit. >0 freezes movement.
    public int rootedStacks = 0;
    public Vector2Int currentCell = new Vector2Int(-99, -99);
    // Multiplicative bonus applied to all incoming damage (stacks additively across sources).
    public float damageTakenMult = 1f;
    // Scales outgoing damage dealt by this enemy (additive debuff stacking; <1 reduces damage).
    public float damageDealtMult = 1f;
    // Scales attack interval (additive stacking; >1 slows attack rate).
    public float attackIntervalMult = 1f;

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

    // ── Charm (Bard's Ballad) ──────────────────────────────────────────────────
    [System.NonSerialized] public bool isCharmed           = false;
    // Set permanently by ConversionCircleLogic; replaced normal AI with allied behavior.
    [System.NonSerialized] public bool isPermanentlyCharmed = false;
    private GameObject _charmOutlineObj;
    private float      _charmAttackTimer;

    // ── Slow (Viking's Throwing Axe) ──────────────────────────────────────────
    private float     _originalMoveSpeed;
    private Coroutine _slowCoroutine;

    // ── Poison (Assassin's PoisonDagger) ─────────────────────────────────────
    private float     _poisonDmgPerTick = 0f;
    private Coroutine _poisonCoroutine;

    // Cached nearest Linker partner – re-queried only when it dies.
    private EnemyEntity _cachedLinkerPartner;

    // Cached PlayerMovement – avoids GetComponent<> call inside Update every frame.
    private PlayerMovement _playerMovement;

    // Scratch list reused by the per-frame separation pass to avoid allocation.
    private readonly List<EnemyEntity> _sepScratch = new List<EnemyEntity>();

    // Commander – speed-aura radius
    private float _commanderAuraTimer = 0f;
    // Transient speed bonus granted by a Commander aura; decays when not refreshed.
    [System.NonSerialized] public float commanderSpeedBonus = 0f;
    private float _commanderBonusDecayTimer = 0f;

    // Reflector – shield active


    // ── Visuals ────────────────────────────────────────────────────────────────
    public EnemyTier tier = EnemyTier.Normal;
    private float      _maxHp;
    private GameObject _hpBarRoot;
    private Transform  _hpBarFill;
    private float      _hpVisTimer;
    private static Sprite _pixelSprite;

    // Cached HP-bar visibility settings – refreshed once at run start (and when
    // the player changes settings) instead of calling PlayerPrefs every 0.5 s.
    private static bool _showHpNormal   = false;
    private static bool _showHpElite = true;
    private static bool _showHpBoss     = true;
    public static void RefreshHPBarSettings() {
        _showHpNormal   = PlayerPrefs.GetInt("showHpNormal",   0) == 1;
        _showHpElite = PlayerPrefs.GetInt("showHpElite", 1) == 1;
        _showHpBoss     = PlayerPrefs.GetInt("showHpBoss",     1) == 1;
    }

    void Start() {
        _sr = GetComponent<SpriteRenderer>();
        OutlineManager.Instance?.Register(_sr);
        if (SurvivorMasterScript.Instance.nemesis.isPendingRevenge &&
            behavior == SurvivorMasterScript.Instance.nemesis.killerType) {
            transform.localScale *= 2; hp *= 5; // Nemesis Buff
        }
        _maxHp = hp;
        _pufferBaseScale  = transform.localScale.x;
        var _attack = GetComponent<EnemyAttack>();
        _pufferBaseDamage = _attack != null ? _attack.damage : 10f;
        _originalMoveSpeed = moveSpeed;
        _playerMovement = SurvivorMasterScript.Instance.player.GetComponent<PlayerMovement>();
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

        // Enemies that are off-screen neither chase the player nor respond to their
        // position – they simply stop until they scroll back into view.
        // Linkers are exempted: they must maintain pair spacing even off-screen so
        // their beam doesn't break when the leader moves out of view.
        if (!SurvivorMasterScript.IsOnScreen(transform.position) && behavior != EnemyBehavior.Linker) return;

        Vector3 pPos = SurvivorMasterScript.Instance.player.position;
        Vector3 dir  = (pPos - transform.position).normalized;
        float   dist = Vector3.Distance(transform.position, pPos);

        // Decay Commander aura bonus — if no Commander refreshes it within 4 s it fades out.
        if (commanderSpeedBonus > 0f) {
            _commanderBonusDecayTimer -= Time.deltaTime;
            if (_commanderBonusDecayTimer <= 0f) commanderSpeedBonus = 0f;
        }

        float   s    = rootedStacks > 0 ? 0f
                     : (moveSpeed + commanderSpeedBonus) * (SurvivorMasterScript.Instance.isInsideGraveyard ? 1.57f : 1f);

        // ── Jockey control-reversal (inverts input via PlayerMovement hack) ──
        if (_jockeyLatched) {
            _jockeyTimer -= Time.deltaTime;
            if (_jockeyTimer <= 0f) {
                _jockeyLatched = false;
                if (_playerMovement != null) _playerMovement.inputScale = 1f;
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
        if (isPermanentlyCharmed) { HandlePermanentCharmBehavior(); return; }
        if (isCharmed)            { HandleCharmedBehavior();        return; }
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
                        if (e != null && !e.isDead && e != this) e.Heal(10f);
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
                    if (_playerMovement != null) StartCoroutine(TempSlow(_playerMovement, 0.5f, 2f));
                }
                break;

            // ── Swarmer – fast, low damage, chases directly ───────────────────
            case EnemyBehavior.Swarmer:
                transform.position += dir * (s * 1.6f) * Time.deltaTime;
                if (dist < attackRange) SurvivorMasterScript.Instance.TakeDamage(3f * Time.deltaTime);
                break;

            // ── ShieldBearer – only moves when player faces away ──────────────
            case EnemyBehavior.ShieldBearer: {
                Vector2 playerFacing = _playerMovement?.LastFacing ?? Vector2.down;
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

            // ── Commander – buff nearby enemy speed (transient, decays without refresh) ──
            case EnemyBehavior.Commander:
                // Hang back 8 units
                if (dist < 8f)       transform.position -= dir * s * 0.5f * Time.deltaTime;
                else if (dist > 14f) transform.position += dir * s * 0.4f * Time.deltaTime;
                _commanderAuraTimer -= Time.deltaTime;
                if (_commanderAuraTimer <= 0f) {
                    _commanderAuraTimer = 3f;
                    foreach (var e in SurvivorMasterScript.Instance.Grid.GetNearby(transform.position)) {
                        if (e == null || e.isDead || e == this) continue;
                        // Grant a flat bonus capped at 50% of the target's base speed.
                        // Writing to commanderSpeedBonus (not moveSpeed) means the buff
                        // disappears 4 s after the Commander dies or moves out of range.
                        e.commanderSpeedBonus = Mathf.Min(
                            e.commanderSpeedBonus + e.moveSpeed * 0.1f,
                            e.moveSpeed * 0.5f);
                        e._commanderBonusDecayTimer = 4f;
                    }
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
                        if (_playerMovement != null) _playerMovement.inputScale = 0.4f;
                    }
                } else {
                    transform.position = pPos; // ride the player
                    _latchTimer -= Time.deltaTime;
                    SurvivorMasterScript.Instance.TakeDamage(2f * Time.deltaTime);
                    if (_latchTimer <= 0f) {
                        _latched = false;
                        if (_playerMovement != null) _playerMovement.inputScale = 1f;
                        transform.position = pPos + (Vector3)(Random.insideUnitCircle * 3f);
                    }
                }
                break;

            // ── Puffer – approach player, grow over time, explode at 350% ─────
            case EnemyBehavior.Puffer: {
                // Move toward the player so it can get in melee range; overlay gentle wander
                if (dist > attackRange) transform.position += dir * s * 0.55f * Time.deltaTime;
                // Normalize so wander magnitude is always 1 (Sin/Cos vector magnitude
                // varies between 1 and √2 unnormalized, causing inconsistent drift).
                Vector3 wander = new Vector3(Mathf.Sin(Time.time * 0.6f + GetInstanceID()),
                                              Mathf.Cos(Time.time * 0.7f + GetInstanceID()), 0f).normalized;
                transform.position += wander * s * 0.25f * Time.deltaTime;

                // Grow +5% of base scale per second; explode at 350%
                if (!_pufferInflated) {
                    float maxScale = _pufferBaseScale * 3.5f;
                    float newScale = transform.localScale.x + _pufferBaseScale * 0.05f * Time.deltaTime;
                    if (newScale >= maxScale) {
                        _pufferInflated = true;
                        // How many 5%-of-base steps have elapsed; capped at the 350% ceiling
                        int growthSteps = Mathf.FloorToInt((transform.localScale.x / _pufferBaseScale - 1f) / 0.05f);
                        // Pool base scale = +400% of puffer base, +10% extra per growth step
                        float poolScale = _pufferBaseScale * (4f + 0.1f * growthSteps);
                        PoisonPoolHazard.Spawn(transform.position, poolScale, _pufferBaseDamage * 2f);
                        TakeDamage(9999f);
                    } else {
                        transform.localScale = Vector3.one * newScale;
                    }
                }
                break;
            }

            // ── Jockey – flank rear, leap (reverse controls) ─────────────────
            case EnemyBehavior.Jockey: {
                Vector2 pf = _playerMovement?.LastFacing ?? Vector2.down;
                Vector3 behindPlayer = pPos - (Vector3)(pf.normalized * 1.5f);
                transform.position = Vector3.MoveTowards(transform.position, behindPlayer, s * Time.deltaTime);
                if (Vector3.Distance(transform.position, behindPlayer) < 0.5f && !_jockeyLatched) {
                    _jockeyLatched = true; _jockeyTimer = 3f;
                    if (_playerMovement != null) _playerMovement.inputScale = -1f;
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
                    if (dist < 1.5f && _playerMovement != null)
                        StartCoroutine(TempSlow(_playerMovement, 0.5f, 2f));
                }
                break;
            }

            // ── Linker – walk so the chosen beam's centre lands on the player ─────
            case EnemyBehavior.Linker: {
                const float MAX_RANGE    = 17.25f;  // must match LinkerBeam.maxBeamRange
                const float TARGET_RANGE = MAX_RANGE * 0.95f;

                // Use the shared LinkerBeam registry for partner lookup – no SpatialGrid
                // cell-boundary gaps. Only re-search when the cached partner is gone.
                if (_cachedLinkerPartner == null || _cachedLinkerPartner.isDead)
                    _cachedLinkerPartner = LinkerBeam.FindNearestFrom(transform.position, this, MAX_RANGE);

                if (_cachedLinkerPartner == null) {
                    // No partner yet – drift toward the player.
                    if (dist > attackRange) transform.position += dir * s * 0.6f * Time.deltaTime;
                    break;
                }

                // Linked linkers move 75% faster to sweep the beam through the player more aggressively.
                s *= 1.75f;

                Vector3 partPos   = _cachedLinkerPartner.transform.position;
                float   partDist  = Vector3.Distance(transform.position, partPos);
                Vector3 toPartner = (partPos - transform.position).normalized;

                // Pair-local leadership: the member with the lower instance ID is the
                // leader of THIS pair. Every pair independently drives its beam midpoint
                // toward the player, so groups of 4+ linkers all stay active – there is
                // no single global leader that leaves the rest frozen.
                bool isPairLeader = GetInstanceID() < _cachedLinkerPartner.GetInstanceID();

                if (isPairLeader) {
                    // Target: position self so (self + partner) / 2 == player.
                    // → self_target = 2*pPos - partPos.
                    // Clamp so beam length never exceeds TARGET_RANGE.
                    Vector3 target   = 2f * pPos - partPos;
                    Vector3 toTarget = target - partPos;  // = 2*(pPos - partPos)
                    if (toTarget.magnitude > TARGET_RANGE)
                        target = partPos + toTarget.normalized * TARGET_RANGE;
                    transform.position = Vector3.MoveTowards(transform.position, target, s * Time.deltaTime);
                } else {
                    // Follower: spring-hold TARGET_RANGE from the pair leader.
                    // diff > 0 → too far → move toward partner; diff < 0 → too close → push away.
                    float diff = partDist - TARGET_RANGE;
                    transform.position += toPartner * (diff * 2f) * Time.deltaTime;
                }
                break;
            }

            // ── Default Chaser ─────────────────────────────────────────────────
            default:
                if (dist > attackRange) transform.position += dir * s * Time.deltaTime;
                break;
        }

        // ── Separation – stop enemies piling directly on top of one another ──
        // Queries the SpatialGrid (no allocation) and applies a small repulsion
        // force from any nearby enemy that's overlapping our personal space.
        // This prevents crowds from creating an impenetrable wall.
        const float SEP_RADIUS = 1.0f;
        const float SEP_FORCE  = 2.0f;
        SurvivorMasterScript.Instance.Grid.GetNearby(transform.position, _sepScratch);
        foreach (var other in _sepScratch) {
            if (other == null || other == this || other.isDead) continue;
            Vector3 away = transform.position - other.transform.position;
            float   d    = away.magnitude;
            if (d > 0f && d < SEP_RADIUS)
                transform.position += away.normalized * SEP_FORCE * (1f - d / SEP_RADIUS) * Time.deltaTime;
        }

        // ── Health bar follow + periodic visibility refresh ──────────────────
        if (_hpBarRoot != null) {
            // Place the bar just above the top edge of the sprite, regardless of sprite height.
            float halfSpriteH = (_sr != null && _sr.sprite != null)
                ? _sr.sprite.bounds.extents.y * transform.localScale.y
                : transform.localScale.y * 0.055f; // fallback: ~half of default unit sprite at scale 10
            _hpBarRoot.transform.position = transform.position + Vector3.up * (halfSpriteH + 0.25f);
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
        var b = SurvivorMasterScript.Instance.BestiaryLookup.TryGetValue(behavior, out var entry) ? entry : null;
        if (b != null && b.isHunterBonusUnlocked) d *= 1.15f;
        d *= damageTakenMult;
        d = Mathf.Round(d);
        SurvivorMasterScript.Instance.RegisterDamageDealt(d);
        FloatingText.SpawnEnemyDamage(transform.position, d);
        hp -= d;
        UpdateHealthBar();
        if (hp <= 0) Die();
    }

    public void Heal(float amount) {
        if (isDead || amount <= 0f) return;
        float prev = hp;
        float maxHp = _maxHp > 0f ? _maxHp : hp;
        hp = Mathf.Min(hp + amount, maxHp);
        float healed = hp - prev;
        if (healed <= 0f) return;

        UpdateHealthBar();
        FloatingText.SpawnHeal(transform.position, healed);
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

    // ── Charm ─────────────────────────────────────────────────────────────────
    public void ApplySlow(float speedMultiplier, float duration) {
        if (_slowCoroutine != null) {
            StopCoroutine(_slowCoroutine);
            moveSpeed = _originalMoveSpeed;
        }
        _slowCoroutine = StartCoroutine(SlowRoutine(speedMultiplier, duration));
    }

    IEnumerator SlowRoutine(float mult, float duration) {
        moveSpeed = _originalMoveSpeed * mult;
        yield return new WaitForSeconds(duration);
        if (!isDead) moveSpeed = _originalMoveSpeed;
        _slowCoroutine = null;
    }

    // Accumulates poison damage per tick. Subsequent applications stack additively on the
    // active per-tick value so rapid stabs build poison without restarting the timer.
    public void ApplyPoison(float dmgPerTick) {
        _poisonDmgPerTick += dmgPerTick;
        if (_poisonCoroutine == null)
            _poisonCoroutine = StartCoroutine(PoisonRoutine());
    }

    IEnumerator PoisonRoutine() {
        yield return new WaitForSeconds(1f);
        if (!isDead) TakeDamage(_poisonDmgPerTick);
        yield return new WaitForSeconds(1f);
        if (!isDead) TakeDamage(_poisonDmgPerTick);
        _poisonDmgPerTick = 0f;
        _poisonCoroutine  = null;
    }

    public void ApplyCharm(float duration) {
        if (isCharmed || isPermanentlyCharmed) return;
        StartCoroutine(CharmRoutine(duration));
    }

    IEnumerator CharmRoutine(float duration) {
        isCharmed = true;
        // Clear Linker partnership so the beam drops immediately.
        if (behavior == EnemyBehavior.Linker) _cachedLinkerPartner = null;

        // Lime silhouette: child sprite scaled 20% larger, drawn behind the enemy.
        _charmOutlineObj = new GameObject("CharmOutline");
        _charmOutlineObj.transform.SetParent(transform);
        _charmOutlineObj.transform.localPosition = Vector3.zero;
        _charmOutlineObj.transform.localScale    = Vector3.one * 1.2f;
        var outSr = _charmOutlineObj.AddComponent<SpriteRenderer>();
        outSr.sprite       = _sr?.sprite;
        outSr.color        = Color.green;
        outSr.sortingLayerName = "Default";
        outSr.sortingOrder = (_sr?.sortingOrder ?? 5) - 1;

        yield return new WaitForSeconds(duration);

        isCharmed = false;
        if (_charmOutlineObj != null) { Destroy(_charmOutlineObj); _charmOutlineObj = null; }
    }

    void HandleCharmedBehavior() {
        // Find the nearest non-charmed, non-dead enemy to fight.
        var nearby = SurvivorMasterScript.Instance.Grid.GetNearby(transform.position);
        EnemyEntity foe    = null;
        float       bestSq = float.MaxValue;
        foreach (var e in nearby) {
            if (e == null || e.isDead || e == this || e.isCharmed || e.isPermanentlyCharmed) continue;
            float sq = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; foe = e; }
        }

        if (foe != null) {
            Vector3 dir = (foe.transform.position - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;

            _charmAttackTimer -= Time.deltaTime;
            if (bestSq < 1.5f * 1.5f && _charmAttackTimer <= 0f) {
                _charmAttackTimer = 1f;
                float dmg = GetComponent<EnemyAttack>()?.damage ?? 5f;
                foe.TakeDamage(dmg);
            }
        }

        // Keep HP bar following the enemy.
        if (_hpBarRoot != null) {
            float halfH = (_sr != null && _sr.sprite != null)
                ? _sr.sprite.bounds.extents.y * transform.localScale.y
                : transform.localScale.y * 0.055f;
            _hpBarRoot.transform.position = transform.position + Vector3.up * (halfH + 0.25f);
        }
    }

    // Allied behavior for permanently converted enemies.
    // On-screen: attack nearby hostile enemies. Off-screen OR no hostiles found: walk toward player.
    void HandlePermanentCharmBehavior() {
        Vector3 pPos     = SurvivorMasterScript.Instance.player.position;
        bool    onScreen = SurvivorMasterScript.IsOnScreen(transform.position);

        EnemyEntity foe   = null;
        float       bestSq = float.MaxValue;

        if (onScreen) {
            foreach (var e in SurvivorMasterScript.Instance.Grid.GetNearby(transform.position)) {
                if (e == null || e.isDead || e == this) continue;
                if (e.isCharmed || e.isPermanentlyCharmed) continue;
                float sq = ((Vector2)(e.transform.position - transform.position)).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; foe = e; }
            }
        }

        if (foe != null) {
            transform.position += (foe.transform.position - transform.position).normalized * moveSpeed * Time.deltaTime;
            _charmAttackTimer -= Time.deltaTime;
            if (bestSq < 1.5f * 1.5f && _charmAttackTimer <= 0f) {
                _charmAttackTimer = 1f;
                foe.TakeDamage(GetComponent<EnemyAttack>()?.damage ?? 5f);
            }
        } else {
            transform.position += (pPos - transform.position).normalized * moveSpeed * Time.deltaTime;
        }

        if (_hpBarRoot != null) {
            float halfH = (_sr != null && _sr.sprite != null)
                ? _sr.sprite.bounds.extents.y * transform.localScale.y
                : transform.localScale.y * 0.055f;
            _hpBarRoot.transform.position = transform.position + Vector3.up * (halfH + 0.25f);
        }
    }

    void Die() {
        isDead = true;
        if (isPermanentlyCharmed)
            ConversionCircleLogic.CharmedCount = Mathf.Max(0, ConversionCircleLogic.CharmedCount - 1);
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
        OutlineManager.Instance?.Unregister(_sr);
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

        float initHalfH = (_sr != null && _sr.sprite != null)
            ? _sr.sprite.bounds.extents.y * transform.localScale.y
            : transform.localScale.y * 0.055f;
        _hpBarRoot.transform.position = transform.position + Vector3.up * (initHalfH + 0.25f);
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
            case EnemyTier.Elite: return _showHpElite;
            case EnemyTier.Boss:     return _showHpBoss;
            default:                 return _showHpNormal;
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