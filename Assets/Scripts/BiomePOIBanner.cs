using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton banner that displays a biome or POI name at the top-centre of the screen
/// for roughly 3 seconds, then fades out. Calling Show() again interrupts any
/// in-progress fade and immediately shows the new label.
/// </summary>
public class BiomePOIBanner : MonoBehaviour {

    public static BiomePOIBanner Instance { get; private set; }

    private Text       _label;
    private Coroutine  _fadeRoutine;

    const float HoldTime    = 0.6f;  // seconds at full opacity before fading
    const float FadeDuration = 2.4f; // seconds to fade from 1 → 0  (total visible ~3 s)

    // ── Bootstrap ─────────────────────────────────────────────────────────────

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    static void EnsureInstance() {
        if (Instance != null) return;
        Debug.Log("[BiomePOIBanner] Creating new singleton instance.");
        new GameObject("BiomePOIBanner").AddComponent<BiomePOIBanner>();
    }

    void BuildUI() {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Debug.Log($"[BiomePOIBanner] BuildUI — font null? {font == null}");

        // Canvas must be a root-level GameObject for ScreenSpaceOverlay to render reliably.
        // We call DontDestroyOnLoad on it separately so it persists alongside this object.
        var cgo    = new GameObject("BiomeBannerCanvas");
        DontDestroyOnLoad(cgo);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        cgo.AddComponent<CanvasScaler>();
        Debug.Log($"[BiomePOIBanner] Canvas created, sortingOrder={canvas.sortingOrder}");

        // Text element — anchored to the top-centre of the screen
        var tgo = new GameObject("BannerLabel");
        tgo.transform.SetParent(canvas.transform, false);

        _label            = tgo.AddComponent<Text>();
        _label.font        = font;
        _label.fontSize    = 30;
        _label.fontStyle   = FontStyle.Bold;
        _label.alignment   = TextAnchor.UpperCenter;
        _label.color       = new Color(1f, 1f, 0.75f, 0f); // fully transparent until shown
        Debug.Log($"[BiomePOIBanner] Text component created, font null? {_label.font == null}");

        var rt             = tgo.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0f, 1f);
        rt.anchorMax       = new Vector2(1f, 1f);
        rt.pivot           = new Vector2(0.5f, 1f);
        rt.sizeDelta       = new Vector2(0f, 60f);
        rt.anchoredPosition = new Vector2(0f, -10f);
        Debug.Log("[BiomePOIBanner] BuildUI complete.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows <paramref name="rawLabel"/> in the banner, interrupting any active display.
    /// CamelCase strings are auto-split into words.
    /// </summary>
    public static void Show(string rawLabel) {
        Debug.Log($"[BiomePOIBanner] Show(\"{rawLabel}\") called. Instance null? {Instance == null}");
        EnsureInstance();
        Instance.ShowLabel(FormatLabel(rawLabel));
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    void ShowLabel(string text) {
        Debug.Log($"[BiomePOIBanner] ShowLabel(\"{text}\") — _label null? {_label == null}");
        if (_label == null) return;
        _label.text = text;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine() {
        Debug.Log($"[BiomePOIBanner] FadeRoutine started for \"{_label.text}\"");
        SetAlpha(1f);
        yield return new WaitForSeconds(HoldTime);

        float elapsed = 0f;
        while (elapsed < FadeDuration) {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / FadeDuration));
            yield return null;
        }

        SetAlpha(0f);
        _fadeRoutine = null;
    }

    void SetAlpha(float a) {
        if (_label == null) return;
        Color c = _label.color;
        c.a = a;
        _label.color = c;
    }

    /// <summary>Inserts a space before each uppercase letter that follows a lowercase
    /// letter or digit, turning "HolyShrine" into "Holy Shrine".</summary>
    static string FormatLabel(string raw) {
        if (string.IsNullOrEmpty(raw)) return raw;
        var sb = new StringBuilder(raw.Length + 8);
        for (int i = 0; i < raw.Length; i++) {
            if (i > 0 && char.IsUpper(raw[i]) && !char.IsWhiteSpace(raw[i - 1]))
                sb.Append(' ');
            sb.Append(raw[i]);
        }
        return sb.ToString();
    }
}
