using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class GameSessionController : MonoBehaviour
{
    [Header("Game time")]
    [SerializeField, Min(10f)] private float gameDuration = 60f;

    [Header("Rank thresholds")]
    [SerializeField, Min(0)] private int rankAScore = 5000;
    [SerializeField, Min(0)] private int rankBScore = 2500;

    private PanTossController panController;
    private SlowMotionCommandSystem commandSystem;
    private DragonRiseTechnique dragonTechnique;
    private TornadoSpinTechnique tornadoTechnique;
    private Text timerText;
    private Text countdownText;
    private GameObject resultRoot;
    private Text resultRankText;
    private Text resultScoreText;
    private float remainingTime;
    private bool gameRunning;
    private bool waitingToFinish;
    private bool resultShown;

    private void Awake()
    {
        panController = GetComponent<PanTossController>();
        commandSystem = GetComponent<SlowMotionCommandSystem>();
        dragonTechnique = GetComponent<DragonRiseTechnique>();
        tornadoTechnique = GetComponent<TornadoSpinTechnique>();
        BuildUi();
    }

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        remainingTime = gameDuration;
        panController.SetSessionLocked(true);
        timerText.text = FormatTime(remainingTime);

        string[] countdown = { "3", "2", "1", "START!" };
        foreach (string value in countdown)
        {
            countdownText.text = value;
            yield return new WaitForSecondsRealtime(value == "START!" ? 0.65f : 0.75f);
        }

        countdownText.gameObject.SetActive(false);
        gameRunning = true;
        panController.SetSessionLocked(false);
    }

    private void Update()
    {
        if (gameRunning)
        {
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            timerText.text = FormatTime(remainingTime);
            if (remainingTime <= 0f)
                BeginFinish();
        }
        else if (waitingToFinish)
        {
            if (tornadoTechnique == null)
                tornadoTechnique = GetComponent<TornadoSpinTechnique>();
            if (!commandSystem.IsBusy && !dragonTechnique.IsPerforming &&
                (tornadoTechnique == null || !tornadoTechnique.IsPerforming))
                ShowResult();
        }
        else if (resultShown && RetryPressed())
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void BeginFinish()
    {
        gameRunning = false;
        waitingToFinish = true;
        panController.SetSessionLocked(true);
        timerText.text = "TIME UP";
        countdownText.gameObject.SetActive(true);
        countdownText.text = "TIME UP!";
    }

    private void ShowResult()
    {
        waitingToFinish = false;
        resultShown = true;
        countdownText.gameObject.SetActive(false);
        timerText.gameObject.SetActive(false);

        int finalScore = commandSystem.CurrentScore;
        string rank = finalScore >= rankAScore ? "A" : finalScore >= rankBScore ? "B" : "C";
        resultRankText.text = rank;
        resultRankText.color = rank == "A"
            ? new Color(1f, 0.63f, 0.08f)
            : rank == "B" ? new Color(0.45f, 0.8f, 1f) : Color.white;
        resultScoreText.text = "SCORE  " + finalScore.ToString("D7");
        resultRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("GameSessionUI");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        timerText = CreateText("Timer", canvas.transform, new Vector2(0.5f, 0.96f), 42);
        timerText.fontStyle = FontStyle.Bold;
        timerText.rectTransform.sizeDelta = new Vector2(400f, 60f);
        AddOutline(timerText, 2f);

        countdownText = CreateText("Countdown", canvas.transform, new Vector2(0.5f, 0.53f), 108);
        countdownText.fontStyle = FontStyle.Bold;
        countdownText.rectTransform.sizeDelta = new Vector2(900f, 150f);
        countdownText.color = new Color(1f, 0.52f, 0.06f);
        AddOutline(countdownText, 4f);

        resultRoot = new GameObject("ResultScreen");
        resultRoot.transform.SetParent(canvas.transform, false);
        RectTransform resultRect = resultRoot.AddComponent<RectTransform>();
        resultRect.anchorMin = Vector2.zero;
        resultRect.anchorMax = Vector2.one;
        resultRect.offsetMin = Vector2.zero;
        resultRect.offsetMax = Vector2.zero;
        Image shade = resultRoot.AddComponent<Image>();
        shade.color = new Color(0.015f, 0.01f, 0.025f, 0.88f);

        Text title = CreateText("ResultTitle", resultRoot.transform, new Vector2(0.5f, 0.78f), 58);
        title.text = "RESULT";
        title.fontStyle = FontStyle.Bold;
        AddOutline(title, 3f);

        resultRankText = CreateText("Rank", resultRoot.transform, new Vector2(0.5f, 0.56f), 160);
        resultRankText.fontStyle = FontStyle.Bold;
        resultRankText.rectTransform.sizeDelta = new Vector2(500f, 190f);
        AddOutline(resultRankText, 5f);

        resultScoreText = CreateText("FinalScore", resultRoot.transform, new Vector2(0.5f, 0.36f), 46);
        resultScoreText.fontStyle = FontStyle.Bold;
        AddOutline(resultScoreText, 3f);

        Text retry = CreateText("Retry", resultRoot.transform, new Vector2(0.5f, 0.18f), 30);
        retry.text = "PRESS  R  TO RETRY";
        retry.color = new Color(0.8f, 0.8f, 0.85f);
        AddOutline(retry, 2f);
        resultRoot.SetActive(false);
    }

    private static Text CreateText(string name, Transform parent, Vector2 anchor, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 100f);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void AddOutline(Text text, float distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(distance, -distance);
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(seconds);
        return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
    }

    private static bool RetryPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
