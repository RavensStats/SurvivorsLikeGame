using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that applies a shared SpriteOutline material to every registered SpriteRenderer.
/// All renderers share the same Material instance so one SetFloat call toggles everyone.
/// </summary>
public class OutlineManager : MonoBehaviour
{
    public static OutlineManager Instance { get; private set; }

    private Material _mat;
    private readonly List<SpriteRenderer> _tracked = new List<SpriteRenderer>();
    private bool _enabled;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Shader shader = Shader.Find("Custom/SpriteOutline");
        if (shader == null)
        {
            Debug.LogWarning("[OutlineManager] SpriteOutline shader not found — outlines disabled.");
            return;
        }
        _mat = new Material(shader);
        _mat.SetFloat("_OutlineWidth", 1.5f);

        _enabled = PlayerPrefs.GetInt("highVisibility", 0) == 1;
        _mat.SetFloat("_OutlineEnabled", _enabled ? 1f : 0f);
    }

    public void Register(SpriteRenderer sr)
    {
        if (sr == null || _mat == null) return;
        sr.sharedMaterial = _mat;
        if (!_tracked.Contains(sr)) _tracked.Add(sr);
    }

    public void Unregister(SpriteRenderer sr)
    {
        if (sr == null) return;
        _tracked.Remove(sr);
    }

    public void SetEnabled(bool on)
    {
        _enabled = on;
        if (_mat != null)
            _mat.SetFloat("_OutlineEnabled", on ? 1f : 0f);
    }
}
