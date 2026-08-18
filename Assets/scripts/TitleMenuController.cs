using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
public sealed class TitleMenuController : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0.025f, 0.035f, 0.055f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.11f, 0.12f, 0.15f, 0.98f);
    private static readonly Color HighlightColor = new Color(0.88f, 0.28f, 0.08f, 1f);

    private void Awake()
    {
        BuildEventSystem();
        BuildTitleUi();
    }

    private static void BuildEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private void BuildTitleUi()
    {
        GameObject canvasObject = new GameObject("TitleCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Image background = CreateImage("Background", canvasObject.transform, BackgroundColor);
        Stretch(background.rectTransform);

        Text title = CreateText("Title", canvasObject.transform, "超次元炒飯", 76);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.76f);
        titleRect.anchorMax = new Vector2(0.5f, 0.76f);
        titleRect.sizeDelta = new Vector2(1000f, 130f);
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1f, 0.72f, 0.16f, 1f);
        Outline titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0.35f, 0.03f, 0f, 1f);
        titleOutline.effectDistance = new Vector2(4f, -4f);

        GameObject menuObject = new GameObject("MainMenuButtons");
        menuObject.transform.SetParent(canvasObject.transform, false);
        RectTransform menuRect = menuObject.AddComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.43f);
        menuRect.anchorMax = new Vector2(0.5f, 0.43f);
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.sizeDelta = new Vector2(680f, 360f);

        VerticalLayoutGroup layout = menuObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 26f;
        layout.padding = new RectOffset(20, 20, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Button storyButton = CreateMenuButton(menuObject.transform, "StoryModeButton", "ストーリーモード");
        Button cookingButton = CreateMenuButton(menuObject.transform, "CookingModeButton", "料理モード");
        Button optionButton = CreateMenuButton(menuObject.transform, "OptionsButton", "オプション");

        storyButton.onClick.AddListener(OpenStoryMode);
        cookingButton.onClick.AddListener(OpenCookingMode);
        optionButton.onClick.AddListener(() => Debug.Log("オプション：遷移先は未設定です。"));
        storyButton.Select();
    }

    private static void OpenCookingMode()
    {
        SceneManager.LoadScene("FriedRicePrototype");
    }

    private static void OpenStoryMode()
    {
        SceneManager.LoadScene("StoryScene");
    }

    private static Button CreateMenuButton(Transform parent, string objectName, string label)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = ButtonColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = new Color(0.34f, 0.13f, 0.07f, 1f);
        colors.selectedColor = new Color(0.42f, 0.15f, 0.06f, 1f);
        colors.pressedColor = HighlightColor;
        colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 92f;
        layout.minHeight = 92f;

        Outline border = buttonObject.AddComponent<Outline>();
        border.effectColor = new Color(1f, 0.48f, 0.1f, 0.9f);
        border.effectDistance = new Vector2(3f, -3f);

        Text text = CreateText("Label", buttonObject.transform, label, 38);
        Stretch(text.rectTransform);
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
