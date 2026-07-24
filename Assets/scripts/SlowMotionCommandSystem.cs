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
    private enum TechniqueType
    {
        DragonRise,
        TornadoSpin,
        Water,
        Lightning
    }

    public int CurrentScore => score;
    public bool IsBusy => acceptingInput || judgmentShowing;
    public event Action<bool> CommandFinished;
    public event Action DragonRiseRequested;
    public event Action TornadoSpinRequested;

    [SerializeField, Range(0.05f, 0.5f)] private float slowScale = 0.18f;
    [SerializeField, Min(0.5f)] private float commandTime = 3.5f;

    [Header("Technique gauge")]
    [SerializeField, Range(1f, 100f)] private float goodGaugeGain = 5f;
    [SerializeField, Range(1f, 100f)] private float greatGaugeGain = 10f;
    [SerializeField, Range(1f, 100f)] private float perfectGaugeGain = 15f;

    [Header("Toss timing")]
    [Tooltip("PERFECTの中心。鍋振り終了から次に振るまでの秒数。")]
    [SerializeField, Min(0f)] private float perfectLandingDelay = 0.45f;
    [SerializeField, Range(0.05f, 0.5f)] private float perfectTimingWindow = 0.16f;
    [SerializeField, Range(0.1f, 1f)] private float greatTimingWindow = 0.38f;

    [Header("Score and cooldown")]
    [SerializeField, Min(1f)] private float cooldownDuration = 10f;
    [SerializeField] private int tossScore = 100;
    [SerializeField] private int dragonRiseSuccessScore = 1000;
    [SerializeField] private int tornadoSpinBaseScore = 300;

    [Header("Editable UI text")]
    [SerializeField] private string gaugeLabel = "TECHNIQUE";
    [SerializeField] private string slowMotionLabel = "SLOW MOTION";
    [SerializeField] private string successLabel = "COMMAND SUCCESS";
    [SerializeField] private string failedLabel = "COMMAND FAILED";
    [SerializeField] private string dragonRiseName = "RYU-SHO-HAN!";
    [SerializeField] private string tornadoSpinName = "TATSUMAKI-SENSHO!";

    [Header("Editable gauge appearance")]
    [SerializeField, Range(20, 72)] private int gaugeFontSize = 42;
    [SerializeField] private Vector2 gaugeAnchor = new Vector2(0.5f, 0.10f);
    [SerializeField] private Vector2 gaugeSize = new Vector2(620f, 62f);
    [SerializeField] private Color gaugeColor = new Color(1f, 0.28f, 0.03f, 1f);
    [SerializeField] private Color gaugeMaxColor = new Color(1f, 0.72f, 0.12f, 1f);

    private readonly string[] dragonRiseLabels = { "A", "D", "W", "SPACE" };
    private readonly string[] tornadoSpinLabels = { "A", "A", "D", "D", "W", "W", "A" };
    private PanTossController panController;
    private Canvas canvas;
    private Text titleText;
    private Text commandText;
    private Text timerText;
    private Text resultText;
    private Image gaugeFill;
    private RectTransform gaugeFillRect;
    private Text gaugeText;
    private Text tossTimingText;
    private Text scoreText;
    private Image dragonCooldownFill;
    private Text dragonCooldownText;
    private Image tornadoCooldownFill;
    private Text tornadoCooldownText;
    private int commandIndex;
    private float remainingTime;
    private float inputArmTime;
    private float normalFixedDeltaTime;
    private bool acceptingInput;
    private bool judgmentShowing;
    private float techniqueGauge;
    private float dragonCooldownElapsed;
    private float tornadoCooldownElapsed;
    private int score;
    private float lastTossFinishedTime;
    private bool hasFinishedPreviousToss;
    private Coroutine tossTimingRoutine;
    private TechniqueType selectedTechnique = TechniqueType.DragonRise;
    private TechniqueType activeTechnique;
    private Texture2D radialTexture;
    private Sprite radialSprite;
    private Texture2D elementTexture;
    private Sprite elementSprite;
    private Image elementDiamond;
    private Text elementNameText;
    private AudioSource elementChangeAudio;
    private AudioClip elementChangeClip;

    private void Awake()
    {
        normalFixedDeltaTime = Time.fixedDeltaTime;
        commandTime = Mathf.Max(commandTime, 3.5f);
        panController = GetComponent<PanTossController>();
        if (GetComponent<TornadoSpinTechnique>() == null)
            gameObject.AddComponent<TornadoSpinTechnique>();
        CreateElementChangeAudio();
        BuildRuntimeUi();
        BuildGaugeUi();
        RefreshGaugeUi();
        dragonCooldownElapsed = 0f;
        tornadoCooldownElapsed = 0f;
        RefreshCornerHud();
    }

    private void OnEnable()
    {
        if (panController != null)
        {
            panController.TossStarted += HandleTossStarted;
            panController.TossFinished += HandleTossFinished;
        }
    }

    private void OnDisable()
    {
        if (panController != null)
        {
            panController.TossStarted -= HandleTossStarted;
            panController.TossFinished -= HandleTossFinished;
        }
        if (panController != null) panController.SetControlLocked(false);
        RestoreTime();
    }

    private void OnDestroy()
    {
        if (radialSprite != null) Destroy(radialSprite);
        if (radialTexture != null) Destroy(radialTexture);
        if (elementSprite != null) Destroy(elementSprite);
        if (elementTexture != null) Destroy(elementTexture);
        if (elementChangeClip != null) Destroy(elementChangeClip);
    }

    private void Update()
    {
        UpdateCooldown();
        if (!acceptingInput)
        {
            if (!judgmentShowing)
                UpdateElementSelection();

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
            if (commandIndex >= ActiveLabels.Length) Complete(true, ActiveTechniqueName);
        }
        else if (AnyCommandKeyPressed())
        {
            Complete(false, "MISS!");
        }
    }

    private void HandleTossStarted()
    {
        if (acceptingInput || (panController != null && panController.IsTechniqueToss))
            return;

        float elapsedAfterLanding = hasFinishedPreviousToss
            ? Time.unscaledTime - lastTossFinishedTime
            : float.PositiveInfinity;
        float timingError = Mathf.Abs(elapsedAfterLanding - perfectLandingDelay);
        string rating;
        float gaugeGain;
        Color ratingColor;

        if (hasFinishedPreviousToss && timingError <= perfectTimingWindow)
        {
            rating = "PERFECT";
            gaugeGain = perfectGaugeGain;
            ratingColor = new Color(1f, 0.72f, 0.08f);
        }
        else if (hasFinishedPreviousToss && timingError <= greatTimingWindow)
        {
            rating = "GREAT";
            gaugeGain = greatGaugeGain;
            ratingColor = new Color(0.25f, 0.9f, 1f);
        }
        else
        {
            rating = "GOOD";
            gaugeGain = goodGaugeGain;
            ratingColor = Color.white;
        }

        techniqueGauge = Mathf.Min(100f, techniqueGauge + gaugeGain);
        score += tossScore;
        RefreshGaugeUi();
        RefreshCornerHud();
        ShowTossTiming(rating, gaugeGain, ratingColor);

    }

    private void HandleTossFinished()
    {
        if (panController != null && panController.IsTechniqueToss)
            return;
        lastTossFinishedTime = Time.unscaledTime;
        hasFinishedPreviousToss = true;
    }

    private void ShowTossTiming(string rating, float gaugeGain, Color color)
    {
        if (tossTimingText == null)
            return;

        tossTimingText.text = rating + "  +" + Mathf.RoundToInt(gaugeGain) + "%";
        tossTimingText.color = color;
        tossTimingText.enabled = true;
        if (tossTimingRoutine != null)
            StopCoroutine(tossTimingRoutine);
        tossTimingRoutine = StartCoroutine(HideTossTiming());
    }

    private IEnumerator HideTossTiming()
    {
        yield return new WaitForSecondsRealtime(0.65f);
        if (tossTimingText != null)
            tossTimingText.enabled = false;
        tossTimingRoutine = null;
    }

    private void BeginCommand()
    {
        if (acceptingInput) return;
        if (panController != null) panController.SetControlLocked(true);
        StopAllCoroutines();
        tossTimingRoutine = null;
        if (tossTimingText != null)
            tossTimingText.enabled = false;
        activeTechnique = selectedTechnique;
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
            score += activeTechnique == TechniqueType.DragonRise
                ? dragonRiseSuccessScore
                : tornadoSpinBaseScore;
            if (activeTechnique == TechniqueType.DragonRise)
                dragonCooldownElapsed = 0f;
            else
                tornadoCooldownElapsed = 0f;
            RefreshCornerHud();
            if (activeTechnique == TechniqueType.DragonRise)
                DragonRiseRequested?.Invoke();
            else
                TornadoSpinRequested?.Invoke();
        }
    }

    public void AddTechniqueScore(int points)
    {
        if (points <= 0)
            return;

        score += points;
        RefreshCornerHud();
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
        string expected = commandIndex < ActiveLabels.Length ? ActiveLabels[commandIndex] : string.Empty;
        if (expected == "A") return Keyboard.current.aKey.wasPressedThisFrame;
        if (expected == "D") return Keyboard.current.dKey.wasPressedThisFrame;
        if (expected == "W") return Keyboard.current.wKey.wasPressedThisFrame;
        if (expected == "SPACE") return Keyboard.current.spaceKey.wasPressedThisFrame;
        return false;
#else
        string expected = commandIndex < ActiveLabels.Length ? ActiveLabels[commandIndex] : string.Empty;
        if (expected == "A") return Input.GetKeyDown(KeyCode.A);
        if (expected == "D") return Input.GetKeyDown(KeyCode.D);
        if (expected == "W") return Input.GetKeyDown(KeyCode.W);
        if (expected == "SPACE") return Input.GetKeyDown(KeyCode.Space);
        return false;
#endif
    }

    private string[] ActiveLabels => activeTechnique == TechniqueType.DragonRise
        ? dragonRiseLabels
        : tornadoSpinLabels;

    private string ActiveTechniqueName => activeTechnique == TechniqueType.DragonRise
        ? dragonRiseName
        : tornadoSpinName;

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

    private void UpdateElementSelection()
    {
        if (FireElementPressed()) ChangeElement(TechniqueType.DragonRise);
        else if (WindElementPressed()) ChangeElement(TechniqueType.TornadoSpin);
        else if (WaterElementPressed()) ChangeElement(TechniqueType.Water);
        else if (LightningElementPressed()) ChangeElement(TechniqueType.Lightning);
    }

    private void ChangeElement(TechniqueType element)
    {
        if (selectedTechnique == element)
            return;

        selectedTechnique = element;
        if (elementChangeAudio != null && elementChangeClip != null)
            elementChangeAudio.PlayOneShot(elementChangeClip, 0.72f);
        RefreshElementDiamond();
        RefreshGaugeUi();
    }

    private static bool FireElementPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.digit1Key.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Alpha1);
#endif
    }

    private static bool WindElementPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.digit2Key.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Alpha2);
#endif
    }

    private static bool WaterElementPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.DownArrow);
#endif
    }

    private static bool LightningElementPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.RightArrow);
#endif
    }

    private void RefreshCommandText()
    {
        var builder = new StringBuilder();
        string[] labels = ActiveLabels;
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
        gaugeText = CreateText("GaugeLabel", root.transform, labelAnchor, Mathf.Min(gaugeFontSize, 30));
        gaugeText.fontStyle = FontStyle.Bold;
        gaugeText.rectTransform.sizeDelta = new Vector2(1160f, 60f);
        Outline outline = gaugeText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        tossTimingText = CreateText("TossTimingText", root.transform,
            gaugeAnchor + new Vector2(0f, 0.14f), 38);
        tossTimingText.fontStyle = FontStyle.Bold;
        tossTimingText.rectTransform.sizeDelta = new Vector2(700f, 70f);
        tossTimingText.enabled = false;
        Outline timingOutline = tossTimingText.gameObject.AddComponent<Outline>();
        timingOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        timingOutline.effectDistance = new Vector2(3f, -3f);

        BuildCornerHud(root.transform);
        BuildElementDiamondUi(root.transform);
    }

    private void BuildElementDiamondUi(Transform hudRoot)
    {
        GameObject diamondObject = new GameObject("ElementDiamond");
        diamondObject.transform.SetParent(hudRoot, false);
        RectTransform rect = diamondObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(34f, 42f);
        rect.sizeDelta = new Vector2(176f, 176f);
        elementDiamond = diamondObject.AddComponent<Image>();
        elementDiamond.raycastTarget = false;

        elementTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        elementTexture.name = "Runtime Element Diamond";
        elementTexture.filterMode = FilterMode.Bilinear;
        elementSprite = Sprite.Create(elementTexture, new Rect(0f, 0f, 128f, 128f),
            new Vector2(0.5f, 0.5f), 128f);
        elementDiamond.sprite = elementSprite;

        CreateElementLabel("FireLabel", "火  ↑", hudRoot, new Vector2(122f, 210f));
        CreateElementLabel("WindLabel", "←  風", hudRoot, new Vector2(48f, 132f));
        CreateElementLabel("WaterLabel", "水  ↓", hudRoot, new Vector2(122f, 55f));
        CreateElementLabel("LightningLabel", "雷  →", hudRoot, new Vector2(197f, 132f));

        elementNameText = CreateText("SelectedElementName", hudRoot, Vector2.zero, 23);
        elementNameText.fontStyle = FontStyle.Bold;
        elementNameText.alignment = TextAnchor.MiddleCenter;
        elementNameText.rectTransform.pivot = Vector2.zero;
        elementNameText.rectTransform.anchoredPosition = new Vector2(10f, 230f);
        elementNameText.rectTransform.sizeDelta = new Vector2(260f, 38f);
        Outline nameOutline = elementNameText.gameObject.AddComponent<Outline>();
        nameOutline.effectColor = Color.black;
        nameOutline.effectDistance = new Vector2(2f, -2f);
        RefreshElementDiamond();
    }

    private static void CreateElementLabel(string name, string label, Transform parent, Vector2 position)
    {
        Text text = CreateText(name, parent, Vector2.zero, 21);
        text.text = label;
        text.fontStyle = FontStyle.Bold;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = new Vector2(90f, 34f);
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void RefreshElementDiamond()
    {
        if (elementTexture == null)
            return;

        Color fire = ElementColor(TechniqueType.DragonRise, new Color(1f, 0.16f, 0.03f));
        Color wind = ElementColor(TechniqueType.TornadoSpin, new Color(0.08f, 0.95f, 0.22f));
        Color water = ElementColor(TechniqueType.Water, new Color(0.05f, 0.48f, 1f));
        Color lightning = ElementColor(TechniqueType.Lightning, new Color(1f, 0.82f, 0.04f));
        Color[] pixels = new Color[elementTexture.width * elementTexture.height];
        for (int y = 0; y < elementTexture.height; y++)
        {
            for (int x = 0; x < elementTexture.width; x++)
            {
                float nx = (x + 0.5f) / elementTexture.width * 2f - 1f;
                float ny = (y + 0.5f) / elementTexture.height * 2f - 1f;
                float distance = Mathf.Abs(nx) + Mathf.Abs(ny);
                Color color = Color.clear;
                if (distance <= 1f)
                {
                    if (distance > 0.92f || Mathf.Abs(Mathf.Abs(nx) - Mathf.Abs(ny)) < 0.025f)
                        color = new Color(0.015f, 0.015f, 0.02f, 1f);
                    else if (Mathf.Abs(nx) > Mathf.Abs(ny))
                        color = nx < 0f ? wind : lightning;
                    else
                        color = ny > 0f ? fire : water;
                }
                pixels[y * elementTexture.width + x] = color;
            }
        }
        elementTexture.SetPixels(pixels);
        elementTexture.Apply();

        if (elementNameText == null)
            return;
        if (selectedTechnique == TechniqueType.DragonRise)
            elementNameText.text = "FIRE  /  RYU-SHO-HAN";
        else if (selectedTechnique == TechniqueType.TornadoSpin)
            elementNameText.text = "WIND  /  TATSUMAKI-SENSHO";
        else if (selectedTechnique == TechniqueType.Water)
            elementNameText.text = "WATER  /  NO TECHNIQUE";
        else
            elementNameText.text = "LIGHTNING  /  NO TECHNIQUE";
    }

    private Color ElementColor(TechniqueType element, Color baseColor)
    {
        return selectedTechnique == element
            ? baseColor
            : Color.Lerp(new Color(0.03f, 0.035f, 0.04f, 1f), baseColor, 0.28f);
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

        Sprite cooldownSprite = CreateRuntimeSquareSprite();
        CreateCooldownHud(hudRoot, "DragonRise", new Vector2(-38f, -32f),
            new Color(0.9f, 0.05f, 0.025f, 1f), "RYU-SHO",
            cooldownSprite, out dragonCooldownFill, out dragonCooldownText);
        CreateCooldownHud(hudRoot, "TornadoSpin", new Vector2(-174f, -32f),
            new Color(0.08f, 0.9f, 0.2f, 1f), "TORNADO",
            cooldownSprite, out tornadoCooldownFill, out tornadoCooldownText);
    }

    private static void CreateCooldownHud(Transform hudRoot, string prefix, Vector2 position,
        Color fillColor, string label, Sprite sprite, out Image fill, out Text statusText)
    {
        GameObject backgroundObject = new GameObject(prefix + "CooldownBackground");
        backgroundObject.transform.SetParent(hudRoot, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.one;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.pivot = Vector2.one;
        backgroundRect.anchoredPosition = position;
        backgroundRect.sizeDelta = new Vector2(112f, 112f);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.015f, 0.015f, 0.02f, 0.94f);

        GameObject fillObject = new GameObject(prefix + "CooldownRadialFill");
        fillObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.06f, 0.06f);
        fillRect.anchorMax = new Vector2(0.94f, 0.94f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fill = fillObject.AddComponent<Image>();
        fill.sprite = sprite;
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = 2;
        fill.fillClockwise = true;
        fill.fillAmount = 0f;

        Text nameText = CreateText(prefix + "CooldownName", hudRoot, Vector2.one, 17);
        nameText.text = label;
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.UpperRight;
        nameText.rectTransform.pivot = Vector2.one;
        nameText.rectTransform.anchoredPosition = position + new Vector2(0f, -116f);
        nameText.rectTransform.sizeDelta = new Vector2(112f, 25f);

        statusText = CreateText(prefix + "CooldownStatus", hudRoot, Vector2.one, 21);
        statusText.fontStyle = FontStyle.Bold;
        statusText.alignment = TextAnchor.UpperRight;
        statusText.rectTransform.pivot = Vector2.one;
        statusText.rectTransform.anchoredPosition = position + new Vector2(0f, -140f);
        statusText.rectTransform.sizeDelta = new Vector2(112f, 36f);
        Outline outline = statusText.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
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
            ? SelectedTechniqueLabel + (CooldownReady() ? "  MAX!  [P]" : "  MAX!  WAIT")
            : SelectedTechniqueLabel + "  " + Mathf.RoundToInt(techniqueGauge) + "%";
        gaugeText.color = techniqueGauge >= 100f
            ? gaugeMaxColor
            : Color.white;
    }

    private string SelectedTechniqueLabel
    {
        get
        {
            if (selectedTechnique == TechniqueType.DragonRise)
                return gaugeLabel + "  FIRE / RYU-SHO-HAN";
            if (selectedTechnique == TechniqueType.TornadoSpin)
                return gaugeLabel + "  WIND / TATSUMAKI-SENSHO";
            if (selectedTechnique == TechniqueType.Water)
                return gaugeLabel + "  WATER / NO TECHNIQUE";
            return gaugeLabel + "  LIGHTNING / NO TECHNIQUE";
        }
    }

    private void UpdateCooldown()
    {
        bool changed = false;
        if (dragonCooldownElapsed < cooldownDuration)
        {
            dragonCooldownElapsed = Mathf.Min(
                cooldownDuration, dragonCooldownElapsed + Time.unscaledDeltaTime);
            changed = true;
        }
        if (tornadoCooldownElapsed < cooldownDuration)
        {
            tornadoCooldownElapsed = Mathf.Min(
                cooldownDuration, tornadoCooldownElapsed + Time.unscaledDeltaTime);
            changed = true;
        }
        if (changed)
        {
            RefreshCornerHud();
            RefreshGaugeUi();
        }
    }

    private bool CooldownReady()
    {
        if (selectedTechnique == TechniqueType.DragonRise)
            return dragonCooldownElapsed >= cooldownDuration;
        if (selectedTechnique == TechniqueType.TornadoSpin)
            return tornadoCooldownElapsed >= cooldownDuration;
        return false;
    }

    private void CreateElementChangeAudio()
    {
        elementChangeAudio = gameObject.AddComponent<AudioSource>();
        elementChangeAudio.playOnAwake = false;
        elementChangeAudio.loop = false;
        elementChangeAudio.spatialBlend = 0f;
        elementChangeAudio.volume = 0.9f;
        elementChangeClip = CreateElementChangeClip();
    }

    private static AudioClip CreateElementChangeClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float attack = Mathf.Clamp01(time / 0.008f);
            float decay = Mathf.Exp(-time * 8.5f);
            float shimmer = Mathf.Sin(2f * Mathf.PI * 1320f * time) * 0.42f
                + Mathf.Sin(2f * Mathf.PI * 1980f * time) * 0.25f
                + Mathf.Sin(2f * Mathf.PI * 2640f * time) * 0.14f;
            float strike = Mathf.Sin(2f * Mathf.PI * 740f * time) * Mathf.Exp(-time * 20f) * 0.35f;
            samples[i] = Mathf.Clamp((shimmer * decay + strike) * attack, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Element Change Chakiin", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void RefreshCornerHud()
    {
        if (scoreText != null)
            scoreText.text = "SCORE  " + score.ToString("D7");

        if (dragonCooldownFill == null || dragonCooldownText == null ||
            tornadoCooldownFill == null || tornadoCooldownText == null)
            return;

        RefreshCooldownDisplay(dragonCooldownFill, dragonCooldownText, dragonCooldownElapsed,
            new Color(1f, 0.72f, 0.12f));
        RefreshCooldownDisplay(tornadoCooldownFill, tornadoCooldownText, tornadoCooldownElapsed,
            new Color(0.35f, 1f, 0.35f));
    }

    private void RefreshCooldownDisplay(Image fill, Text statusText, float elapsed, Color readyColor)
    {
        float ratio = cooldownDuration > 0f ? Mathf.Clamp01(elapsed / cooldownDuration) : 1f;
        fill.fillAmount = ratio;
        if (ratio >= 1f)
        {
            statusText.text = "READY";
            statusText.color = readyColor;
        }
        else
        {
            statusText.text = (cooldownDuration - elapsed).ToString("0.0") + "s";
            statusText.color = Color.white;
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
