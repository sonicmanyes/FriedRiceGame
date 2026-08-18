using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StorySceneObjectBuilder
{
    private const string ScenePath = "Assets/Scenes/StoryScene.unity";
    private const string RootName = "StoryMapCanvas";

    private static readonly Vector2[] StagePositions =
    {
        new Vector2(-650f, -155f),
        new Vector2(-350f, 75f),
        new Vector2(-35f, -90f),
        new Vector2(290f, 115f),
        new Vector2(650f, -35f)
    };

    [InitializeOnLoadMethod]
    private static void ScheduleBuild()
    {
        EditorApplication.update -= WaitUntilEditorIsReady;
        EditorApplication.update += WaitUntilEditorIsReady;
    }

    private static void WaitUntilEditorIsReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= WaitUntilEditorIsReady;
        BuildIfNeeded();
    }

    [MenuItem("Fried Rice/Build Story Scene Objects")]
    public static void BuildIfNeeded()
    {
        if (Application.isPlaying || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            return;

        Scene storyScene = FindLoadedStoryScene();
        bool openedForBuild = !storyScene.IsValid();
        if (openedForBuild)
            storyScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GameObject storyRoot = FindRoot(storyScene, RootName);
        bool changed = false;
        if (storyRoot == null)
        {
            storyRoot = BuildObjects(storyScene);
            changed = true;
        }

        if (ApplyCurrentAppearance(storyRoot))
            changed = true;

        if (storyRoot.GetComponent<StoryStageSelector>() == null)
        {
            storyRoot.AddComponent<StoryStageSelector>();
            changed = true;
        }

        if (EnsurePlayerIcon(storyRoot))
            changed = true;

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(storyScene);
            EditorSceneManager.SaveScene(storyScene);
            Debug.Log("StorySceneのステージUIオブジェクトを更新しました。");
        }

        if (openedForBuild)
            EditorSceneManager.CloseScene(storyScene, true);
    }

    private static GameObject BuildObjects(Scene scene)
    {
        GameObject canvasObject = new GameObject(RootName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvasObject.transform.localScale = Vector3.one;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage("MapBackground", canvasObject.transform, Color.clear);
        Stretch(background.rectTransform);

        Text heading = CreateText("StoryHeading", canvasObject.transform, "STORY MODE", 58);
        heading.rectTransform.anchorMin = new Vector2(0.5f, 0.88f);
        heading.rectTransform.anchorMax = new Vector2(0.5f, 0.88f);
        heading.rectTransform.anchoredPosition = Vector2.zero;
        heading.rectTransform.sizeDelta = new Vector2(800f, 90f);
        heading.fontStyle = FontStyle.Bold;
        heading.color = new Color(1f, 0.95f, 0.82f, 1f);

        GameObject routes = CreateUiRoot("StageRoutes", canvasObject.transform);
        for (int i = 0; i < StagePositions.Length - 1; i++)
            CreateRouteLine(routes.transform, i + 1, StagePositions[i], StagePositions[i + 1]);

        GameObject nodes = CreateUiRoot("StageNodes", canvasObject.transform);
        Sprite nodeSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (nodeSprite == null)
            nodeSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        for (int i = 0; i < StagePositions.Length; i++)
            CreateStageNode(nodes.transform, nodeSprite, i, StagePositions[i], i == StagePositions.Length - 1);

        return canvasObject;
    }

    private static void CreateStageNode(
        Transform parent, Sprite sprite, int index, Vector2 position, bool isBoss)
    {
        string nodeName = isBoss ? "BossStageNode" : "StageNode_" + (index + 1);
        GameObject nodeObject = new GameObject(nodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        nodeObject.transform.SetParent(parent, false);
        RectTransform rect = nodeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = isBoss ? new Vector2(230f, 150f) : new Vector2(200f, 132f);

        Image image = nodeObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = isBoss
            ? new Color(0.82f, 0.08f, 0.055f, 1f)
            : new Color(0.98f, 0.98f, 0.96f, 1f);

        Outline outline = nodeObject.AddComponent<Outline>();
        outline.effectColor = isBoss
            ? new Color(0.32f, 0.015f, 0.01f, 1f)
            : new Color(0.28f, 0.31f, 0.32f, 1f);
        outline.effectDistance = new Vector2(5f, -5f);

    }

    private static bool ApplyCurrentAppearance(GameObject storyRoot)
    {
        if (storyRoot == null) return false;
        bool changed = false;

        Transform backgroundTransform = storyRoot.transform.Find("MapBackground");
        if (backgroundTransform != null && backgroundTransform.TryGetComponent(out Image background) &&
            background.color != Color.clear)
        {
            background.color = Color.clear;
            changed = true;
        }

        Transform nodes = storyRoot.transform.Find("StageNodes");
        if (nodes == null) return changed;
        for (int i = 0; i < nodes.childCount; i++)
        {
            Transform label = nodes.GetChild(i).Find("StageLabel");
            if (label == null) continue;
            Object.DestroyImmediate(label.gameObject);
            changed = true;
        }

        return changed;
    }

    private static bool EnsurePlayerIcon(GameObject storyRoot)
    {
        Transform nodes = storyRoot.transform.Find("StageNodes");
        if (nodes == null) return false;

        Sprite idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/IDLEIcon.png");
        Sprite pressSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Scenes/MainPressIcon.png");
        Transform playerTransform = nodes.Find("PlayerIcon");
        bool changed = false;
        Image playerImage;
        if (playerTransform == null)
        {
            GameObject playerObject = new GameObject("PlayerIcon",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            playerObject.transform.SetParent(nodes, false);
            playerTransform = playerObject.transform;
            RectTransform playerRect = playerObject.GetComponent<RectTransform>();
            playerRect.anchorMin = new Vector2(0.5f, 0.5f);
            playerRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerRect.pivot = new Vector2(0.5f, 0f);
            playerRect.anchoredPosition = StagePositions[0] + new Vector2(0f, 35f);
            playerRect.sizeDelta = new Vector2(200f, 200f);
            playerImage = playerObject.GetComponent<Image>();
            playerImage.preserveAspect = true;
            playerImage.raycastTarget = false;
            changed = true;
        }
        else
        {
            playerImage = playerTransform.GetComponent<Image>();
        }

        if (playerImage != null && playerImage.sprite != idleSprite)
        {
            playerImage.sprite = idleSprite;
            changed = true;
        }

        StoryStageSelector selector = storyRoot.GetComponent<StoryStageSelector>();
        if (selector == null) return changed;
        SerializedObject selectorData = new SerializedObject(selector);
        SerializedProperty playerProperty = selectorData.FindProperty("playerIcon");
        SerializedProperty idleProperty = selectorData.FindProperty("idleIcon");
        SerializedProperty pressProperty = selectorData.FindProperty("mainPressIcon");
        if (playerProperty.objectReferenceValue != playerImage ||
            idleProperty.objectReferenceValue != idleSprite ||
            pressProperty.objectReferenceValue != pressSprite)
        {
            playerProperty.objectReferenceValue = playerImage;
            idleProperty.objectReferenceValue = idleSprite;
            pressProperty.objectReferenceValue = pressSprite;
            selectorData.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        return changed;
    }

    private static void CreateRouteLine(Transform parent, int number, Vector2 start, Vector2 end)
    {
        GameObject lineObject = new GameObject("RouteLine_" + number,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(parent, false);
        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 direction = end - start;
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.sizeDelta = new Vector2(direction.magnitude, 9f);
        rect.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        Image line = lineObject.GetComponent<Image>();
        line.color = new Color(1f, 1f, 1f, 0.88f);
        line.raycastTarget = false;
    }

    private static GameObject CreateUiRoot(string name, Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        return root;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize)
    {
        GameObject textObject = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Scene FindLoadedStoryScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == ScenePath)
                return scene;
        }
        return default;
    }

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        if (!scene.IsValid()) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == rootName) return root;
        return null;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
