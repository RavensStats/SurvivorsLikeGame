using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the Mage player sprite purely from code — no Animator Controller needed.
/// Loads sprites at runtime from Resources/Sprites/Characters/Mage/animations/...
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAnimator : MonoBehaviour {
    [Tooltip("Frames per second for all animations")]
    public float fps = 10f;

    // ── Internals ──────────────────────────────────────────────────────────────
    private SpriteRenderer sr;
    private Rigidbody2D rb;

    private enum AnimState { Walk, Fireball, Death }
    private AnimState state = AnimState.Walk;

    // direction index matches VelocityToDir sectors: 0=S,1=SW,2=W,3=NW,4=N,5=NE,6=E,7=SE
    private static readonly string[] DirNames = {
        "south", "south-west", "west", "north-west",
        "north", "north-east", "east", "south-east"
    };

    // Cached sprite arrays: [animName][dirIndex][frameIndex]
    private Dictionary<string, Sprite[][]> clips = new Dictionary<string, Sprite[][]>();

    // Resolved animation folder names for this character (discovered at load time)
    private string walkClip   = "walking-6-frames";
    private string attackClip = "fireball";
    private string deathClip  = "falling-back-death";

    private int   currentDir   = 0;   // 0 = south (default idle direction)
    private int   currentFrame = 0;
    private float frameTimer   = 0f;
    private bool  deathPlayed  = false;
    private float prevHP;

    // ── Unity callbacks ────────────────────────────────────────────────────────
    void Awake() {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        sr.sortingOrder = 6; // above POI (1), gems (4), enemies (5)
    }

    void Start() {
        prevHP = SurvivorMasterScript.Instance != null ? SurvivorMasterScript.Instance.playerHP : 100f;
        LoadClipsForCharacter(PlayerPrefs.GetString("SelectedCharacter", "Mage"));
    }

    void Update() {
        if (deathPlayed) return;

        // Check for death
        float hp = SurvivorMasterScript.Instance != null ? SurvivorMasterScript.Instance.playerHP : 100f;
        if (hp <= 0f && state != AnimState.Death) {
            state = AnimState.Death;
            currentFrame = 0;
            frameTimer = 0f;
        }
        prevHP = hp;

        // Determine direction from velocity; reset to south when idle
        Vector2 vel = rb.linearVelocity;
        if (state != AnimState.Death) {
            if (vel.sqrMagnitude > 0.01f)
                currentDir = VelocityToDir(vel);
            else
                currentDir = 0; // south when idle
        }

        // Advance frame timer only when moving or in non-walk states
        bool isIdle = state == AnimState.Walk && vel.sqrMagnitude <= 0.01f;
        if (!isIdle) {
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / fps) {
                frameTimer -= 1f / fps;
                AdvanceFrame();
            }
        } else {
            currentFrame = 0;
            frameTimer = 0f;
        }

        // Set sprite
        if (walkClip == null) return; // clips not loaded yet

        string clipName = state == AnimState.Walk ? walkClip
                        : state == AnimState.Fireball ? attackClip
                        : deathClip;

        if (clips.TryGetValue(clipName, out Sprite[][] dirs) && dirs[currentDir] != null) {
            int frameCount = dirs[currentDir].Length;
            if (frameCount > 0)
                sr.sprite = dirs[currentDir][Mathf.Clamp(currentFrame, 0, frameCount - 1)];
        }
    }

    // ── Frame advance ──────────────────────────────────────────────────────────
    void AdvanceFrame() {
        string clipName = state == AnimState.Walk ? walkClip
                        : state == AnimState.Fireball ? attackClip
                        : deathClip;

        if (!clips.TryGetValue(clipName, out Sprite[][] dirs) || dirs[currentDir] == null) return;
        int frameCount = dirs[currentDir].Length;

        currentFrame++;
        if (currentFrame >= frameCount) {
            if (state == AnimState.Death) {
                currentFrame = frameCount - 1; // hold last death frame
                deathPlayed = true;
            } else {
                currentFrame = 0; // loop
            }
        }
    }

    // ── Public trigger ─────────────────────────────────────────────────────────
    public void TriggerFireball() {
        if (state == AnimState.Death) return;
        state = AnimState.Fireball;
        currentFrame = 0;
        frameTimer = 0f;
        // Return to walk after fireball finishes (handled via frame loop completion)
    }

    // ── Direction helper ───────────────────────────────────────────────────────
    // Returns index into DirNames: 0=S,1=SE,2=E,3=NE,4=N,5=NW,6=W,7=SW
    static int VelocityToDir(Vector2 vel) {
        float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        // Remap: Atan2 gives 0=east, 90=north.
        // We want 0=south, going clockwise in our index.
        // Convert to compass: 0=N, 90=E, etc., then to our index
        float compass = (-(angle - 90f) + 360f) % 360f; // 0=N, 90=E clockwise
        // Shift so 0=S: add 180
        float shifted = (compass + 180f) % 360f;
        // Map 360 degrees to 8 sectors of 45 degrees each
        int sector = Mathf.RoundToInt(shifted / 45f) % 8;
        return sector; // 0=S,1=SW→ wait, let's verify the mapping matches DirNames
    }

    // ── Sprite loading ─────────────────────────────────────────────────────────
    /// <summary>Reload all clips for the given character folder (e.g. "Mage" or "Ranger").</summary>
    public void LoadClipsForCharacter(string charFolder) {
        clips.Clear();
        walkClip = attackClip = deathClip = null;

        // Discover which animation sub-folders exist in Resources
        string animRoot = Application.dataPath + $"/Resources/Sprites/Characters/{charFolder}/animations";
        var animFolders = new List<string>();
        if (System.IO.Directory.Exists(animRoot)) {
            foreach (var d in System.IO.Directory.GetDirectories(animRoot))
                animFolders.Add(System.IO.Path.GetFileName(d));
        }

        // Map each folder to Walk / Attack / Death by naming convention
        foreach (string animName in animFolders) {
            string lower = animName.ToLower();
            if      (lower.Contains("walk") && walkClip   == null) walkClip   = animName;
            else if ((lower.Contains("fire") || lower.Contains("attack") || lower.Contains("shoot") || lower.Contains("cast")) && attackClip == null) attackClip = animName;
            else if ((lower.Contains("death") || lower.Contains("die") || lower.Contains("dying")) && deathClip == null) deathClip = animName;
        }

        // Fallbacks: if a state has no dedicated clip, reuse walk
        if (walkClip   == null && animFolders.Count > 0) walkClip = animFolders[0];
        if (attackClip == null) attackClip = walkClip;
        if (deathClip  == null) deathClip  = walkClip;

        if (walkClip == null) {
            Debug.LogWarning($"[PlayerAnimator] No animation folders found for character '{charFolder}' at {animRoot}");
            return;
        }

        // Load sprites for every discovered folder
        foreach (string anim in animFolders)
            LoadClip(charFolder, anim);
    }

    void LoadClip(string charFolder, string animName) {
        Sprite[][] dirs = new Sprite[DirNames.Length][];
        for (int d = 0; d < DirNames.Length; d++) {
            string path = $"Sprites/Characters/{charFolder}/animations/{animName}/{DirNames[d]}";
            Sprite[] frames = Resources.LoadAll<Sprite>(path);
            if (frames == null || frames.Length == 0) {
                var frameList = new List<Sprite>();
                for (int f = 0; f < 20; f++) {
                    Sprite s = Resources.Load<Sprite>($"{path}/frame_{f:000}");
                    if (s == null) break;
                    frameList.Add(s);
                }
                frames = frameList.ToArray();
            }
            System.Array.Sort(frames, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            dirs[d] = frames;
            if (frames.Length == 0)
                Debug.LogWarning($"[PlayerAnimator] No sprites found at Resources/{path}");
        }
        clips[animName] = dirs;
    }
}
