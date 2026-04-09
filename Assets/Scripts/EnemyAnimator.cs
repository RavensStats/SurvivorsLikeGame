using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives enemy sprites at runtime with no Animator Controller.
/// Loaded from Resources/Sprites/Enemies/{typeName}/animations/
/// Folder naming conventions: walk keywords = "walk|run|move", attack keywords = "jab|attack|strike|shoot",
/// death keywords = "death|die|dying|crouching"
/// Set up by calling LoadClipsForEnemy(typeName) from EnemySpawner after instantiation.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyAnimator : MonoBehaviour {

    public float fps = 10f;

    private float attackFps = 10f; // overridden per-attack to fit within attackInterval

    // direction index: 0=S,1=SW,2=W,3=NW,4=N,5=NE,6=E,7=SE
    private static readonly string[] DirNames = {
        "south", "south-west", "west", "north-west",
        "north", "north-east", "east", "south-east"
    };

    private enum AnimState { Walk, Attack, Dead }
    private AnimState state = AnimState.Walk;

    private SpriteRenderer sr;
    private EnemyEntity entity;

    // Cached sprite arrays: [animName][dirIndex][frameIndex]
    private Dictionary<string, Sprite[][]> clips = new Dictionary<string, Sprite[][]>();

    private string walkClip   = null;
    private string attackClip = null;
    private string deathClip  = null;

    private int   currentDir   = 0;
    private int   currentFrame = 0;
    private float frameTimer   = 0f;

    void Awake() {
        sr     = GetComponent<SpriteRenderer>();
        entity = GetComponent<EnemyEntity>();
    }

    void Update() {
        if (walkClip == null) return;

        // Transition to dead state
        if (entity != null && entity.isDead && state != AnimState.Dead) {
            state        = AnimState.Dead;
            currentFrame = 0;
            frameTimer   = 0f;
        }

        // Update direction by facing toward player (unless dead)
        if (state != AnimState.Dead && SurvivorMasterScript.Instance != null) {
            Vector2 toPlayer = SurvivorMasterScript.Instance.player.position - transform.position;
            if (toPlayer.sqrMagnitude > 0.01f)
                currentDir = VelocityToDir(toPlayer);
        }

        // Advance animation
        float activeFps = state == AnimState.Attack ? attackFps : fps;
        frameTimer += Time.deltaTime;
        if (frameTimer >= 1f / activeFps) {
            frameTimer -= 1f / activeFps;
            AdvanceFrame();
        }

        // Set sprite
        string clipName = state == AnimState.Walk   ? walkClip
                        : state == AnimState.Attack ? attackClip
                        : deathClip;

        if (clips.TryGetValue(clipName, out Sprite[][] dirs)) {
            // Try current direction, fall back to south (0) if missing frames
            Sprite[] frames = dirs[currentDir];
            if (frames == null || frames.Length == 0) frames = dirs[0];
            if (frames != null && frames.Length > 0) {
                sr.sprite = frames[Mathf.Clamp(currentFrame, 0, frames.Length - 1)];
                if (!sr.enabled) sr.enabled = true; // reveal once first sprite is ready
            }
        }
    }

    void AdvanceFrame() {
        string clipName = state == AnimState.Walk   ? walkClip
                        : state == AnimState.Attack ? attackClip
                        : deathClip;

        if (!clips.TryGetValue(clipName, out Sprite[][] dirs)) return;
        Sprite[] frames = dirs[currentDir];
        if (frames == null || frames.Length == 0) frames = dirs[0];
        if (frames == null || frames.Length == 0) return;

        currentFrame++;
        if (currentFrame >= frames.Length) {
            if (state == AnimState.Dead) {
                currentFrame = frames.Length - 1; // hold last frame
            } else if (state == AnimState.Attack) {
                state        = AnimState.Walk;    // return to walk after attack
                currentFrame = 0;
            } else {
                currentFrame = 0; // loop walk
            }
        }
    }

    /// <summary>Trigger attack animation on this enemy.</summary>
    public void TriggerAttack() {
        if (state == AnimState.Dead) return;

        // Calculate fps so the attack animation completes in exactly one attackInterval.
        var attack = GetComponent<EnemyAttack>();
        if (attack != null && attackClip != null && clips.TryGetValue(attackClip, out Sprite[][] dirs)) {
            Sprite[] frames = dirs[currentDir];
            if (frames == null || frames.Length == 0) frames = dirs[0];
            int frameCount = frames != null ? frames.Length : 1;
            attackFps = frameCount / Mathf.Max(attack.attackInterval, 0.05f);
        }

        state         = AnimState.Attack;
        currentFrame  = 0;
        frameTimer    = 0f;
    }

    // ─── Sprite loading ───────────────────────────────────────────────────────

    /// <summary>Load all animation clips for the given enemy type folder (e.g. "Creeper").</summary>
    public void LoadClipsForEnemy(string typeName) {
        clips.Clear();
        walkClip = attackClip = deathClip = null;

        string animRoot = Application.dataPath + $"/Resources/Sprites/Enemies/{typeName}/animations";
        if (!System.IO.Directory.Exists(animRoot)) {
            Debug.LogWarning($"[EnemyAnimator] No animations folder at {animRoot}");
            return;
        }

        var animFolders = new List<string>();
        foreach (var d in System.IO.Directory.GetDirectories(animRoot))
            animFolders.Add(System.IO.Path.GetFileName(d));

        foreach (string animName in animFolders) {
            string lower = animName.ToLower();
            if      ((lower.Contains("walk") || lower.Contains("run")  || lower.Contains("move") || lower.Contains("crouched")) && walkClip   == null) walkClip   = animName;
            else if ((lower.Contains("jab")  || lower.Contains("attack") || lower.Contains("strike") || lower.Contains("shoot"))  && attackClip == null) attackClip = animName;
            else if ((lower.Contains("death") || lower.Contains("die")  || lower.Contains("dying") || lower.Contains("crouching")) && deathClip == null) deathClip  = animName;
        }

        // Fallbacks
        if (walkClip   == null && animFolders.Count > 0) walkClip = animFolders[0];
        if (attackClip == null) attackClip = walkClip;
        if (deathClip  == null) deathClip  = walkClip;

        if (walkClip == null) {
            Debug.LogWarning($"[EnemyAnimator] No animation folders found for enemy '{typeName}'");
            return;
        }

        foreach (string anim in animFolders)
            LoadClip(typeName, anim);
    }

    void LoadClip(string typeName, string animName) {
        Sprite[][] dirs = new Sprite[DirNames.Length][];
        for (int d = 0; d < DirNames.Length; d++) {
            string path = $"Sprites/Enemies/{typeName}/animations/{animName}/{DirNames[d]}";

            // Strategy 1: load all sprites from the folder (works for Single-mode sprites)
            Sprite[] frames = Resources.LoadAll<Sprite>(path);

            // Strategy 2: frame-by-frame fallback — handles Multiple-mode sub-assets (named frame_000_0)
            // and Single-mode sprites (named frame_000)
            if (frames == null || frames.Length == 0) {
                var list = new List<Sprite>();
                for (int f = 0; f < 32; f++) {
                    string basePath = $"{path}/frame_{f:000}";

                    // Try exact name first
                    Sprite s = Resources.Load<Sprite>(basePath);

                    // Unity Multiple-mode sub-sprites get a "_0" suffix appended
                    if (s == null) s = Resources.Load<Sprite>(basePath + "_0");

                    // Final fallback: load raw Texture2D and create a sprite from it
                    if (s == null) {
                        Texture2D tex = Resources.Load<Texture2D>(basePath);
                        if (tex == null) break; // no texture = end of frames
                        s = Sprite.Create(tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                    }

                    if (s == null) break;
                    list.Add(s);
                }
                frames = list.ToArray();
            }

            if (frames.Length > 0)
                System.Array.Sort(frames, (a, b) =>
                    string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            dirs[d] = frames;
        }
        clips[animName] = dirs;
    }

    // direction helper — same mapping as PlayerAnimator
    static int VelocityToDir(Vector2 vel) {
        float angle   = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        float compass = (-(angle - 90f) + 360f) % 360f;
        float shifted = (compass + 180f)         % 360f;
        return Mathf.RoundToInt(shifted / 45f)   % 8;
    }
}
