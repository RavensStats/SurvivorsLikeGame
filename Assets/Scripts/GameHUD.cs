using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour {
    [Header("Icons (assign in Inspector)")]
    public Sprite heartIcon;
    public Sprite gemIcon;
    public Sprite lightningIcon;
    public Sprite clockIcon;
    public Sprite goldIcon;

    private Image hpFill, xpFill, ultFill;
    private Text timerText, goldText, levelText;
    private Sprite circleSprite;
    private RectTransform circleContainerRect;
    private GameObject topBarRoot;
    private GameObject pauseButtonGO;
    private Text pauseButtonLabel;
    private const float yBelowMinimap = -160f; // minimap 140px + 10px gap + 10px offset
    private const float yTopLeft      = -10f;
    private const float slideSpeed    = 100f;

    void Start() {
        circleSprite = CreateCircleSprite(100);

        // Always create a dedicated HUD canvas so it is never a child of the menu canvas
        GameObject cgo = new GameObject("HUDCanvas");
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;
        cgo.AddComponent<CanvasScaler>();
        cgo.AddComponent<GraphicRaycaster>();

        BuildTopRightBar(canvas);
        BuildSideCircles(canvas);
        BuildPauseButton(canvas);
        SetVisible(false); // hidden until Play is pressed
    }

    // ── Top-right: [clock icon] [timer]  [gold icon] [gold] ──────────────────
    void BuildTopRightBar(Canvas canvas) {
        GameObject row = new GameObject("TopRightBar");
        topBarRoot = row;
        row.transform.SetParent(canvas.transform, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(1f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot     = new Vector2(1f, 1f);
        rowRect.sizeDelta = new Vector2(300, 36);
        rowRect.anchoredPosition = new Vector2(-10, -10);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        timerText = AddIconTextPair(row.transform, clockIcon, new Color(1f, 0.95f, 0.5f), "00:00", 80);
        AddSpacer(row.transform, 12);
        levelText = AddLevelBadge(row.transform);
        AddSpacer(row.transform, 12);
        goldText  = AddIconTextPair(row.transform, goldIcon,  new Color(1f, 0.8f, 0.1f), "0", 60);
    }

    Text AddIconTextPair(Transform parent, Sprite icon, Color iconColor, string defaultVal, float labelWidth) {
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(parent, false);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon != null ? icon : circleSprite;
        iconImg.color  = iconColor;
        LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth  = 24;
        iconLE.preferredHeight = 24;

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(parent, false);
        Text txt = textGO.AddComponent<Text>();
        txt.text      = defaultVal;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 18;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        LayoutElement textLE = textGO.AddComponent<LayoutElement>();
        textLE.preferredWidth  = labelWidth;
        textLE.preferredHeight = 28;

        return txt;
    }

    void AddSpacer(Transform parent, float width) {
        GameObject s = new GameObject("Spacer");
        s.transform.SetParent(parent, false);
        LayoutElement le = s.AddComponent<LayoutElement>();
        le.preferredWidth  = width;
        le.preferredHeight = 1;
    }

    Text AddLevelBadge(Transform parent) {
        // Pill-shaped dark badge: "Lv 1"
        GameObject badge = new GameObject("LevelBadge");
        badge.transform.SetParent(parent, false);
        Image bg = badge.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.18f, 0.30f, 0.92f);
        LayoutElement le = badge.AddComponent<LayoutElement>();
        le.preferredWidth  = 62;
        le.preferredHeight = 28;

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(badge.transform, false);
        RectTransform trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        Text txt = textGO.AddComponent<Text>();
        txt.text      = "Lv 1";
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = new Color(0.6f, 1f, 0.7f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
        return txt;
    }

    // ── Left side: circles stacked vertically under the minimap ──────────────
    // Minimap is 140x140 at (10, -10) top-left → bottom edge at y = -150
    void BuildSideCircles(Canvas canvas) {
        GameObject container = new GameObject("CircleContainer");
        container.transform.SetParent(canvas.transform, false);
        circleContainerRect = container.AddComponent<RectTransform>();
        circleContainerRect.anchorMin = new Vector2(0f, 1f);
        circleContainerRect.anchorMax = new Vector2(0f, 1f);
        circleContainerRect.pivot     = new Vector2(0f, 1f);
        circleContainerRect.sizeDelta = new Vector2(80, 260);
        circleContainerRect.anchoredPosition = new Vector2(10, yBelowMinimap);

        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;

        hpFill  = BuildCircle(container.transform, "HP",  new Color(0.9f, 0.2f, 0.2f), heartIcon,     Color.white);
        xpFill  = BuildCircle(container.transform, "XP",  new Color(0.3f, 0.6f, 1.0f), gemIcon,       new Color(0.6f, 0.9f, 1f));
        ultFill = BuildCircle(container.transform, "Ult", new Color(1.0f, 0.8f, 0.1f), lightningIcon, new Color(1f, 0.95f, 0.5f));
    }

    Image BuildCircle(Transform parent, string name, Color fillColor, Sprite icon, Color iconColor) {
        const float size  = 70f;
        const float inset = 8f;

        GameObject go = new GameObject(name + "Circle");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = size;
        le.preferredHeight = size;

        // Dark background disc
        MakeImage(go.transform, "BG",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            circleSprite, new Color(0f, 0f, 0f, 1.0f));

        // Radial fill disc
        Image fillImg = MakeImage(go.transform, "Fill",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            circleSprite, fillColor);
        fillImg.type          = Image.Type.Filled;
        fillImg.fillMethod    = Image.FillMethod.Radial360;
        fillImg.fillOrigin    = (int)Image.Origin360.Top;
        fillImg.fillClockwise = true;
        fillImg.fillAmount    = 1f;

        // Inner disc to create ring effect
        MakeImage(go.transform, "Inner",
            Vector2.zero, Vector2.one,
            new Vector2(inset, inset), new Vector2(-inset, -inset),
            circleSprite, new Color(0.08f, 0.08f, 0.08f, 1f));

        // Center icon
        float iconSize = size * 0.35f;
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin        = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax        = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta        = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon != null ? icon : circleSprite;
        iconImg.color  = icon != null ? iconColor : new Color(iconColor.r, iconColor.g, iconColor.b, 0.4f);

        return fillImg;
    }

    public void SetVisible(bool visible) {
        if (topBarRoot != null) topBarRoot.SetActive(visible);
        if (circleContainerRect != null) circleContainerRect.gameObject.SetActive(visible);
        if (pauseButtonGO != null) pauseButtonGO.SetActive(visible);
    }

    // ── Pause / Resume button ──────────────────────────────────────────────
    void BuildPauseButton(Canvas canvas) {
        pauseButtonGO = new GameObject("PauseButton");
        pauseButtonGO.transform.SetParent(canvas.transform, false);
        RectTransform rt = pauseButtonGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(44f, 36f);
        rt.anchoredPosition = new Vector2(-10f, -54f); // below the timer/gold bar

        Image img = pauseButtonGO.AddComponent<Image>();
        img.color = new Color(0.15f, 0.22f, 0.38f, 0.9f);

        Button btn = pauseButtonGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = img.color;
        cb.highlightedColor = new Color(0.25f, 0.38f, 0.58f, 1f);
        cb.pressedColor     = new Color(0.09f, 0.14f, 0.24f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(OnPauseClicked);

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(pauseButtonGO.transform, false);
        RectTransform trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        pauseButtonLabel = textGO.AddComponent<Text>();
        pauseButtonLabel.text      = "II";
        pauseButtonLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pauseButtonLabel.fontSize  = 20;
        pauseButtonLabel.fontStyle = FontStyle.Bold;
        pauseButtonLabel.color     = Color.white;
        pauseButtonLabel.alignment = TextAnchor.MiddleCenter;
    }

    void OnPauseClicked() {
        if (Time.timeScale > 0f) {
            Time.timeScale = 0f;
            if (pauseButtonLabel != null) pauseButtonLabel.text = ">";
        } else {
            Time.timeScale = 1f;
            if (pauseButtonLabel != null) pauseButtonLabel.text = "II";
        }
    }

    Image MakeImage(Transform parent, string objName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        Sprite sprite, Color color) {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color  = color;
        return img;
    }

    // ── Procedural circle sprite (avoids built-in resource null issues) ───────
    static Sprite CreateCircleSprite(int res) {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float r = res * 0.5f;
        for (int y = 0; y < res; y++) {
            for (int x = 0; x < res; x++) {
                float dx   = x - r + 0.5f;
                float dy   = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist); // 1px soft AA edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update() {
        var g = SurvivorMasterScript.Instance;
        if (g == null) return;

        // Slide circle container based on minimap visibility
        if (circleContainerRect != null) {
            bool minimapOpen = MinimapSystem.Instance != null && MinimapSystem.Instance.IsPanelActive;
            float targetY = minimapOpen ? yBelowMinimap : yTopLeft;
            Vector2 pos = circleContainerRect.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, targetY, slideSpeed * Time.deltaTime);
            circleContainerRect.anchoredPosition = pos;
        }

        if (hpFill  != null) hpFill.fillAmount  = Mathf.Clamp01(g.playerHP / 100f);
        if (xpFill  != null) xpFill.fillAmount  = g.xpMax > 0 ? Mathf.Clamp01(g.xp / g.xpMax) : 0f;
        if (ultFill != null) ultFill.fillAmount  = g.UltCooldown > 0 ? Mathf.Clamp01(g.UltTimer / g.UltCooldown) : 1f;

        if (timerText != null) {
            int secs = (int)g.GameTime;
            timerText.text = $"{secs / 60:00}:{secs % 60:00}";
        }
        if (levelText != null) levelText.text = $"Lv {g.playerLevel}";
        if (goldText != null) goldText.text = SurvivorMasterScript.GlobalGold.ToString();
    }
}
