using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MinimapSystem : MonoBehaviour {
    [Tooltip("Resolution of the minimap render texture")]
    public int textureSize = 256;
    [Tooltip("How many world units the minimap displays across its height")]
    public float minimapWorldSize = 120f;

    public static MinimapSystem Instance { get; private set; }
    public bool IsPanelActive => minimapPanel != null && minimapPanel.activeSelf;

    private Camera minimapCam;
    private RenderTexture rt;
    private GameObject minimapPanel;

    void Start() {
        Instance = this;
        rt = new RenderTexture(textureSize, textureSize, 24);

        // Minimap camera
        GameObject camGO = new GameObject("MinimapCamera");
        minimapCam = camGO.AddComponent<Camera>();
        minimapCam.orthographic = true;
        minimapCam.orthographicSize = minimapWorldSize / 2f;
        minimapCam.targetTexture = rt;
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        minimapCam.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        minimapCam.depth = -2;

        // Always create a dedicated minimap canvas so it is never a child of the menu canvas
        GameObject canvasGO = new GameObject("MinimapCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Outer panel (top-left corner)
        minimapPanel = new GameObject("MinimapPanel");
        GameObject panelGO = minimapPanel;
        panelGO.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(140, 140);
        panelRect.anchoredPosition = new Vector2(10, -10);
        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        // Click to deactivate
        EventTrigger trigger = panelGO.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(_ => panelGO.SetActive(false));
        trigger.triggers.Add(entry);

        // RawImage displaying the render texture
        GameObject rawGO = new GameObject("MinimapDisplay");
        rawGO.transform.SetParent(panelGO.transform, false);
        RectTransform rawRect = rawGO.AddComponent<RectTransform>();
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.offsetMin = new Vector2(4, 4);
        rawRect.offsetMax = new Vector2(-4, -4);
        RawImage raw = rawGO.AddComponent<RawImage>();
        raw.texture = rt;

        // Player dot (always at center since camera follows player)
        GameObject dotGO = new GameObject("PlayerDot");
        dotGO.transform.SetParent(panelGO.transform, false);
        RectTransform dotRect = dotGO.AddComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.sizeDelta = new Vector2(8, 8);
        dotRect.anchoredPosition = Vector2.zero;
        Image dot = dotGO.AddComponent<Image>();
        dot.color = Color.white;
        SetVisible(false); // hidden until Play is pressed
    }

    void LateUpdate() {
        if (minimapCam == null) return;
        Transform player = SurvivorMasterScript.Instance?.player;
        if (player == null) return;
        minimapCam.transform.position = new Vector3(player.position.x, player.position.y, -10f);
    }

    public void SetVisible(bool visible) {
        if (minimapPanel != null) minimapPanel.SetActive(visible);
    }

    void OnDestroy() {
        if (rt != null) rt.Release();
    }
}
