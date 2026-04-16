using UnityEngine;

/// <summary>
/// Cycles through sprite frames on a SpriteRenderer to animate GIF-sourced weapon sprites.
/// Attach to any weapon VFX GameObject with a SpriteRenderer, then call Init with the frames array.
/// </summary>
public class WeaponSpriteAnimator : MonoBehaviour {
    private Sprite[] _frames;
    private SpriteRenderer _sr;
    private int _currentFrame;
    private float _timer;
    private float _frameInterval;

    /// <param name="frames">Sprite frames to cycle through.</param>
    /// <param name="fps">Playback speed when duration is not specified.</param>
    /// <param name="duration">When > 0, overrides fps so all frames play exactly once within this time window.</param>
    public void Init(Sprite[] frames, float fps = 12f, float duration = 0f) {
        _frames = frames;
        float effectiveFps = (duration > 0f && frames != null && frames.Length > 1)
            ? frames.Length / duration
            : fps;
        _frameInterval = effectiveFps > 0f ? 1f / effectiveFps : 1f / 12f;
        _sr            = GetComponent<SpriteRenderer>();
        if (_sr != null && _frames != null && _frames.Length > 0)
            _sr.sprite = _frames[0];
    }

    void Update() {
        if (_frames == null || _frames.Length <= 1 || _sr == null) return;
        _timer += Time.deltaTime;
        if (_timer >= _frameInterval) {
            _timer        -= _frameInterval;
            _currentFrame  = (_currentFrame + 1) % _frames.Length;
            _sr.sprite     = _frames[_currentFrame];
        }
    }
}
