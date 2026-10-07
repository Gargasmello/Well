using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A restart button, built at runtime so nothing needs wiring up in the scene. Hidden until
// Ending calls Show(). The click is handled here rather than through an EventSystem, so the
// project needs no input module for it.
//
// Restarting reloads the active scene, which means Well.unity has to be in the build
// settings - the build index is -1 otherwise.
public class RestartButton : MonoBehaviour
{
    [SerializeField] string label = "Restart";
    [SerializeField] Vector2 size = new Vector2(240f, 68f);
    [SerializeField] Vector2 position = new Vector2(0f, -180f);
    [SerializeField] Color background = new Color32(0xB8, 0xB8, 0xB0, 0xFF);
    [SerializeField] Color textColour = new Color32(0x2A, 0x2A, 0x22, 0xFF);

    Canvas _canvas;
    CanvasGroup _group;
    RectTransform _rect;
    bool _shown;

    void Awake()
    {
        Build();
        _canvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_shown)
            return;

        _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.deltaTime * 2f);

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (RectTransformUtility.RectangleContainsScreenPoint(_rect, mouse.position.ReadValue()))
            Restart();
    }

    public void Show()
    {
        _shown = true;
        _group.alpha = 0f;
        _canvas.gameObject.SetActive(true);
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Build()
    {
        var canvasGo = new GameObject("RestartCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _group = canvasGo.GetComponent<CanvasGroup>();

        var panel = new GameObject("Button", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);

        _rect = panel.GetComponent<RectTransform>();
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = position;
        _rect.sizeDelta = size;

        panel.GetComponent<Image>().color = background;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColour;
        text.fontSize = 34;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
