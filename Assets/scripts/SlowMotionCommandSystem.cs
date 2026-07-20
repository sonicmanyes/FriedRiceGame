using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class SlowMotionCommandSystem : MonoBehaviour
{
    public int CurrentScore => score;
    public bool IsBusy => acceptingInput || judgmentShowing;
    public event Action<bool> CommandFinished;
    public event Action DragonRiseRequested;

    [SerializeField, Range(0.05f, 0.5f)] private float slowScale = 0.18f;
    [SerializeField, Min(0.5f)] private float commandTime = 2.8f;

    [Header("Technique gauge")]
    [SerializeField, Range(1f, 100f)] private float gaugeGainPerToss = 25f;

    [Header("Score and cooldown")]
    [SerializeField, Min(1f)] private float cooldownDuration = 10f;
    [SerializeField] private int tossScore = 100;
    [SerializeField] private int techniqueSuccessScore = 1000;

    [Header("Editable UI text")]
    [SerializeField] private string gaugeLabel = "TECHNIQUE";
    [SerializeField] private string slowMotionLabel = "SLOW MOTION";
    [SerializeField] private string successLabel = "COMMAND SUCCESS";
    [SerializeField] private string failedLabel = "COMMAND FAILED";
    [SerializeField] private string techniqueName = "RYU-SHO-HAN!";

    [Header("Editable gauge appearance")]
    [SerializeField, Range(20, 72)] private int gaugeFontSize = 42;
    [SerializeField] private Vector2 gaugeAnchor = new Vector2(0.5f, 0.10f);
    [SerializeField] private Vector2 gaugeSize = new Vector2(620f, 62f);
    [SerializeField] private Color gaugeColor = new Color(1f, 0.28f, 0.03f, 1f);
    [SerializeField] private Color gaugeMaxColor = new Color(1f, 0.72f, 0.12f, 1f);

    private readonly string[] labels = { "A", "D", "W", "SPACE" };
    private PanTossController panController;
    private Canvas canvas;
    private Text titleText;
    private Text commandText;
    private Text timerText;
    private Text resultText;
    private Image gaugeFill;
    private RectTransform gaugeFillRect;
    private Text gaugeText;
    private Text scoreText;
    private Image cooldownFill;
    private Text cooldownText;
    private int commandIndex;
    private float remainingTime;
    private float inputArmTime;
    private float normalFixedDeltaTime;
    private bool acceptingInput;
    private bool judgmentShowing;
    private float techniqueGauge;
    private float cooldownElapsed;
    private int score;
    private Texture2D radialTexture;
    private Sprite radialSprite;

    private void Awake()
    {
        normalFixedDeltaTime = Time.fixedDeltaTime;
        panController = GetComponent<PanTossController>();
        BuildRuntimeUi();
        BuildGaugeUi();
        RefreshGaugeUi();
        cooldownElapsed = 0f;
        RefreshCornerHud();
    }

    private void OnEnable()
    {
        if (panController != null) panController.TossStarted += HandleTossStarted;
    }

    private void OnDisable()
    {
        if (panController != null) panController.TossStarted -= HandleTossStarted;
        if (panController != null) panController.SetControlLocked(false);
        RestoreTime();
    }

    private void OnDestroy()
    {
        if (radialSprite != null) Destroy(radialSprite);
        if (radialTexture != null) Destroy(radialTexture);
    }

    private void Update()
    {
        UpdateCooldown();
        if (!acceptingInput)
        {
            if (!judgmentShowing && techniqueGauge >= 100f && CooldownReady() &&
                panController != null && panController.CanActivateTechnique && TechniqueButtonPressed())
            {
                if (panController.TryStartTechniqueToss())
                    BeginCommand();
            }
            return;
        }

        remainingTime -= Time.unscaledDeltaTime;
        timerText.text = remainingTime.ToString("0.0") + " sec";
        if (remainingTime <= 0f)
        {
            Complete(false, "TIME UP!");
            return;
        }

        if (Time.unscaledTime < inputArmTime) return;

        if (ExpectedKeyPressed())
        {
            commandIndex++;
            RefreshCommandText();
            if (commandIndex >= labels.Length) Complete(true, techniqueName);
        }
        else if (AnyCommandKeyPressed())
        {
            Complete(false, "MISS!");
        }
    }

    private void HandleTossStarted()
    {
        if (acceptingInput)
            return;

        techniqueGauge = Mathf.Min(100f, techniqueGauge + gaugeGainPerToss);
        score += tossScore;
        RefreshGaugeUi();
        RefreshCornerHud();

    }

    private void BeginCommand()
    {
        if (acceptingInput) return;
        if (panController != null) panController.SetControlLocked(true);
        StopAllCoroutines();
        commandIndex = 0;
        remainingTime = commandTime;
        inputArmTime = Time.unscaledTime + 0.10f;
        acceptingInput = true;
        canvas.gameObject.SetActive(true);
        titleText.text = slowMotionLabel;
        resultText.text = string.Empty;
        timerText.text = remainingTime.ToString("0.0") + " sec";
        RefreshCommandText();
        Time.timeScale = slowScale;
        Time.fixedDeltaTime = normalFixedDeltaTime * slowScale;
    }

    private void Complete(bool success, string message)
    {
        acceptingInput = false;
        judgmentShowing = true;
        // Keep the scene moving in dramatic slow motion while the judgment
        // flashes, instead of freezing the gameplay completely.
        Time.timeScale = slowScale;
        Time.fixedDeltaTime = normalFixedDeltaTime * slowScale;
        resultText.color = success ? new Color(1f, 0.55f, 0.05f) : new Color(1f, 0.18f, 0.12f);
        resultText.text = message;
        titleText.text = success ? successLabel : failedLabel;
        techniqueGauge = 0f;
        RefreshGaugeUi();
        StartCoroutine(ShowJudgmentThenResume(success));
    }

    private IEnumerator ShowJudgmentThenResume(bool success)
    {
        const int flashes = 4;
        const float halfFlashDuration = 0.125f;

        for (int i = 0; i < flashes; i++)
        {
            resultText.enabled = true;
            titleText.enabled = true;
            yield return new WaitForSecondsRealtime(halfFlashDuration);

            resultText.enabled = false;
            titleText.enabled = false;
            yield return new WaitForSecondsRealtime(halfFlashDuration);
        }

        resultText.enabled = true;
        titleText.enabled = true;
        canvas.gameObject.SetActive(false);
        RestoreTime();
        judgmentShowing = false;
        if (panController != null) panController.SetControlLocked(false);

        CommandFinished?.Invoke(success);
        if (success)
        {
            score += techniqueSuccessScore;
            cooldownElapsed = 0f;
            RefreshCornerHud();
            DragonRiseRequested?.Invoke();
        }
    }

    private void RestoreTime()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = normalFixedDeltaTime > 0f ? normalFixedDeltaTime : 0.02f;
    }

    private bool ExpectedKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (commandIndex)
        {
            case 0: return Keyboard.current.aKey.wasPressedThisFrame;
            case 1: return Keyboard.current.dKey.wasPressedThisFrame;
            case 2: return Keyboard.current.wKey.wasPressedThisFrame;
            case 3: return Keyboard.current.spaceKey.wasPressedThisFrame;
            default: return false;
        }
#else
        KeyCode[] keys = { KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.Space };
        return commandIndex < keys.Length && Input.GetKeyDown(keys[commandIndex]);
#endif
    }

    private static bool AnyCommandKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame ||
                Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
               Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private static bool TechniqueButtonPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.P);
#endif
    }

    private void RefreshCommandText()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0) builder.Append("  >  ");
            if (i < commandIndex) builder.Append("<color=#FFB52E>").Append(labels[i]).Append("</color>");
            else if (i == commandIndex) builder.Append("<color=#FFFFFF><b>[").Append(labels[i]).Append("]</b></color>");
            else builder.Append("<color=#777777>").Append(labels[i]).Append("</color>");
        }
        commandText.text = builder.ToString();
    }

    private void BuildRuntimeUi()
    {
        GameObject root = new GameObject("SlowMotionCommandUI");
        root.transform.SetParent(transform, false);
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        root.AddComponent<GraphicRaycaster>();

        Image shade = CreateImage("Backdrop", canvas.transform);
        shade.color = new Color(0.05f, 0.015f, 0.01f, 0.42f);
        titleText = CreateText("Title", canvas.transform, new Vector2(0.5f, 0.68f), 48);
        commandText = CreateText("Command", canvas.transform, new Vector2(0.5f, 0.54f), 58);
        timerText = CreateText("Timer", canvas.transform, new Vector2(0.5f, 0.43f), 34);
        resultText = CreateText("Result", canvas.transform, new Vector2(0.5f, 0.32f), 64);
        root.SetActive(false);
    }

    private void BuildGaugeUi()
    {
        GameObject root = new GameObject("TechniqueGaugeUI");
        root.transform.SetParent(transform, false);
        Canvas gaugeCanvas = root.AddComponent<Canvas>();
        gaugeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gaugeCanvas.sortingOrder = 90;
        gaugeCanvas.pixelPerfect = true;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject("GaugeBackground");
        backgroundObject.transform.SetParent(root.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = gaugeAnchor;
        backgroundRect.anchorMax = gaugeAnchor;
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = gaugeSize;
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.02f, 0.025f, 0.86f);

        GameObject innerObject = new GameObject("GaugeWhiteInterior");
        innerObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform innerRect = innerObject.AddComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.018f, 0.16f);
        innerRect.anchorMax = new Vector2(0.982f, 0.84f);
        innerRect.offsetMin = Vector2.zero;
        innerRect.offsetMax = Vector2.zero;
        Image innerImage = innerObject.AddComponent<Image>();
        innerImage.color = Color.white;

        GameObject fillObject = new GameObject("GaugeRedFill");
        fillObject.transform.SetParent(innerObject.transform, false);
        gaugeFillRect = fillObject.AddComponent<RectTransform>();
        gaugeFillRect.anchorMin = Vector2.zero;
        gaugeFillRect.anchorMax = new Vector2(0f, 1f);
        gaugeFillRect.pivot = new Vector2(0f, 0.5f);
        gaugeFillRect.offsetMin = Vector2.zero;
        gaugeFillRect.offsetMax = Vector2.zero;
        gaugeFill = fillObject.AddComponent<Image>();
        gaugeFill.color = gaugeColor;

        Vector2 labelAnchor = gaugeAnchor + new Vector2(0f, 0.075f);
        gaugeText = CreateText("GaugeLabel", root.transform, labelAnchor, gaugeFontSize);
        gaugeText.fontStyle = FontStyle.Bold;
        gaugeText.rectTransform.sizeDelta = new Vector2(760f, 60f);
        Outline outline = gaugeText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        BuildCornerHud(root.transform);
    }

    private void BuildCornerHud(Transform hudRoot)
    {
        scoreText = CreateText("ScoreText", hudRoot, new Vector2(0f, 1f), 42);
        scoreText.fontStyle = FontStyle.Bold;
        scoreText.alignment = TextAnchor.UpperLeft;
        scoreText.rectTransform.pivot = new Vector2(0f, 1f);
        scoreText.rectTransform.anchoredPosition = new Vector2(34f, -28f);
        scoreText.rectTransform.sizeDelta = new Vector2(520f, 70f);
        Outline scoreOutline = scoreText.gameObject.AddComponent<Outline>();
        scoreOutline.effectColor = Color.black;
        scoreOutline.effectDistance = new Vector2(2f, -2f);

        GameObject cooldownBackgroundObject = new GameObject("TechniqueCooldownBackground");
        cooldownBackgroundObject.transform.SetParent(hudRoot, false);
        RectTransform backgroundRect = cooldownBackgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.one;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.pivot = Vector2.one;
        backgroundRect.anchoredPosition = new Vector2(-38f, -32f);
        backgroundRect.sizeDelta = new Vector2(112f, 112f);
        Image background = cooldownBackgroundObject.AddComponent<Image>();
        background.color = new Color(0.015f, 0.015f, 0.02f, 0.94f);

        GameObject cooldownFillObject = new GameObject("TechniqueCooldownRadialFill");
        cooldownFillObject.transform.SetParent(cooldownBackgroundObject.transform, false);
        RectTransform fillRect = cooldownFillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.06f, 0.06f);
        fillRect.anchorMax = new Vector2(0.94f, 0.94f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        cooldownFill = cooldownFillObject.AddComponent<Image>();
        cooldownFill.sprite = CreateRuntimeSquareSprite();
        cooldownFill.color = new Color(0.9f, 0.05f, 0.025f, 1f);
        cooldownFill.type = Image.Type.Filled;
        cooldownFill.fillMethod = Image.FillMethod.Radial360;
        cooldownFill.fillOrigin = 2;
        cooldownFill.fillClockwise = true;
        cooldownFill.fillAmount = 0f;

        cooldownText = CreateText("TechniqueCooldownText", hudRoot, Vector2.one, 30);
        cooldownText.fontStyle = FontStyle.Bold;
        cooldownText.alignment = TextAnchor.UpperRight;
        cooldownText.rectTransform.pivot = Vector2.one;
        cooldownText.rectTransform.anchoredPosition = new Vector2(-38f, -151f);
        cooldownText.rectTransform.sizeDelta = new Vector2(260f, 50f);
        Outline cooldownOutline = cooldownText.gameObject.AddComponent<Outline>();
        cooldownOutline.effectColor = Color.black;
        cooldownOutline.effectDistance = new Vector2(2f, -2f);
    }

    private Sprite CreateRuntimeSquareSprite()
    {
        radialTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        radialTexture.name = "Runtime Cooldown Square";
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        radialTexture.SetPixels(pixels);
        radialTexture.Apply();
        radialTexture.filterMode = FilterMode.Point;
        radialSprite = Sprite.Create(radialTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return radialSprite;
    }

    private void RefreshGaugeUi()
    {
        if (gaugeFill == null || gaugeFillRect == null || gaugeText == null)
            return;

        float gaugeRatio = Mathf.Clamp01(techniqueGauge / 100f);
        gaugeFillRect.anchorMax = new Vector2(gaugeRatio, 1f);
        gaugeFillRect.offsetMin = Vector2.zero;
        gaugeFillRect.offsetMax = Vector2.zero;
        gaugeText.text = techniqueGauge >= 100f
            ? gaugeLabel + (CooldownReady() ? "  MAX!  [P]" : "  MAX!  WAIT")
            : gaugeLabel + "  " + Mathf.RoundToInt(techniqueGauge) + "%";
        gaugeText.color = techniqueGauge >= 100f
            ? gaugeMaxColor
            : Color.white;
    }

    private void UpdateCooldown()
    {
        if (cooldownElapsed < cooldownDuration)
        {
            cooldownElapsed = Mathf.Min(cooldownDuration, cooldownElapsed + Time.unscaledDeltaTime);
            RefreshCornerHud();
            RefreshGaugeUi();
        }
    }

    private bool CooldownReady()
    {
        return cooldownElapsed >= cooldownDuration;
    }

    private void RefreshCornerHud()
    {
        if (scoreText != null)
            scoreText.text = "SCORE  " + score.ToString("D7");

        if (cooldownFill == null || cooldownText == null)
            return;

        float ratio = cooldownDuration > 0f ? Mathf.Clamp01(cooldownElapsed / cooldownDuration) : 1f;
        cooldownFill.fillAmount = ratio;
        if (ratio >= 1f)
        {
            cooldownText.text = "READY";
            cooldownText.color = new Color(1f, 0.72f, 0.12f);
        }
        else
        {
            cooldownText.text = (cooldownDuration - cooldownElapsed).ToString("0.0") + "s";
            cooldownText.color = Color.white;
        }
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go.AddComponent<Image>();
    }

    private static Text CreateText(string name, Transform parent, Vector2 anchor, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1100f, 110f);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
