using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class StoryStageSelector : MonoBehaviour
{
    public int SelectedStageIndex => selectedIndex;

    [SerializeField, Range(0f, 1f)] private float grayStrength = 0.42f;
    [SerializeField, Min(1f)] private float colorChangeSpeed = 12f;
    [Header("Player icon")]
    [SerializeField] private Image playerIcon;
    [SerializeField] private Sprite idleIcon;
    [SerializeField] private Sprite mainPressIcon;
    [SerializeField, Min(1f)] private float playerMoveSpeed = 9f;

    private readonly Image[] stageImages = new Image[5];
    private readonly RectTransform[] stageRects = new RectTransform[5];
    private readonly Color[] normalColors = new Color[5];
    private int selectedIndex;
    private Vector2 previousMousePosition;
    private Vector2 playerOffset;
    private bool hasMousePosition;
    private bool selectionConfirmed;

    private void Awake()
    {
        Transform nodes = transform.Find("StageNodes");
        if (nodes == null)
        {
            Debug.LogError("StageNodesが見つかりません。", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < stageImages.Length; i++)
        {
            string nodeName = i == stageImages.Length - 1
                ? "BossStageNode"
                : "StageNode_" + (i + 1);
            Transform node = nodes.Find(nodeName);
            if (node == null || !node.TryGetComponent(out Image image))
            {
                Debug.LogError(nodeName + "が見つかりません。", this);
                enabled = false;
                return;
            }

            stageImages[i] = image;
            stageRects[i] = image.rectTransform;
            normalColors[i] = image.color;
        }

        selectedIndex = 0;
        ApplyColorsImmediately();
        if (playerIcon != null)
        {
            // Use the position authored in the scene as the offset from Stage 1.
            // This lets designers move PlayerIcon directly in the Rect Transform.
            playerOffset = playerIcon.rectTransform.anchoredPosition
                - stageRects[0].anchoredPosition;
            playerIcon.sprite = idleIcon;
            playerIcon.rectTransform.anchoredPosition = PlayerTargetPosition();
        }
    }

    private void Update()
    {
        if (!selectionConfirmed)
        {
            UpdateKeyboardSelection();
            UpdateMouseSelection();
        }
        AnimateColors();
        UpdatePlayerIcon();
    }

    private void UpdateKeyboardSelection()
    {
        int direction = 0;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                Keyboard.current.downArrowKey.wasPressedThisFrame)
                direction = 1;
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                     Keyboard.current.upArrowKey.wasPressedThisFrame)
                direction = -1;
        }
#else
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            direction = 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            direction = -1;
#endif
        if (direction != 0)
            selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, stageImages.Length - 1);
    }

    private void UpdateMouseSelection()
    {
        Vector2 mousePosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return;
        mousePosition = Mouse.current.position.ReadValue();
#else
        mousePosition = Input.mousePosition;
#endif
        if (!hasMousePosition)
        {
            previousMousePosition = mousePosition;
            hasMousePosition = true;
            return;
        }

        if ((mousePosition - previousMousePosition).sqrMagnitude < 0.25f)
            return;
        previousMousePosition = mousePosition;

        for (int i = 0; i < stageRects.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(stageRects[i], mousePosition))
            {
                selectedIndex = i;
                return;
            }
        }
    }

    private void AnimateColors()
    {
        float speed = Time.unscaledDeltaTime * colorChangeSpeed;
        for (int i = 0; i < stageImages.Length; i++)
        {
            Color target = i == selectedIndex
                ? Color.Lerp(normalColors[i], Color.gray, grayStrength)
                : normalColors[i];
            stageImages[i].color = Color.Lerp(stageImages[i].color, target, speed);
        }
    }

    private void UpdatePlayerIcon()
    {
        if (playerIcon == null)
            return;

        float moveAmount = Mathf.Clamp01(Time.unscaledDeltaTime * playerMoveSpeed);
        playerIcon.rectTransform.anchoredPosition = Vector2.Lerp(
            playerIcon.rectTransform.anchoredPosition,
            PlayerTargetPosition(),
            moveAmount);

        if (!selectionConfirmed && ConfirmPressed())
        {
            selectionConfirmed = true;
            if (mainPressIcon != null)
                playerIcon.sprite = mainPressIcon;
        }
    }

    private Vector2 PlayerTargetPosition()
    {
        return stageRects[selectedIndex].anchoredPosition + playerOffset;
    }

    private static bool ConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private void ApplyColorsImmediately()
    {
        for (int i = 0; i < stageImages.Length; i++)
        {
            stageImages[i].color = i == selectedIndex
                ? Color.Lerp(normalColors[i], Color.gray, grayStrength)
                : normalColors[i];
        }
    }
}
