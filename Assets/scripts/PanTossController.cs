using System.Collections;
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public sealed class PanTossController : MonoBehaviour
{
    public bool CanActivateTechnique => !tossing && !controlLocked && !sessionLocked;
    public event Action TossStarted;
    public event Action TossFinished;

    [Header("Timing")]
    [SerializeField, Min(0.2f)] private float tossDuration = 0.82f;
    [SerializeField, Range(0.1f, 0.4f)] private float anticipationEnd = 0.27f;
    [SerializeField, Range(0.55f, 0.9f)] private float swingEnd = 0.72f;

    [Header("Arc motion")]
    [Tooltip("How far the pan moves toward the player before the swing.")]
    [SerializeField, Min(0f)] private float pullBackDistance = 0.34f;
    [SerializeField, Min(0f)] private float dipDepth = 0.24f;
    [SerializeField, Min(0f)] private float forwardDistance = 0.48f;
    [SerializeField, Min(0f)] private float arcHeight = 0.58f;

    [Header("Pan rotation")]
    [SerializeField, Range(0f, 25f)] private float anticipationTilt = 9f;
    [SerializeField, Range(0f, 50f)] private float upwardSnapAngle = 31f;

    private Rigidbody body;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 restForward;
    private Vector3 restUp;
    private bool tossing;
    private bool controlLocked;
    private bool sessionLocked;

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;
    }

    public void SetSessionLocked(bool locked)
    {
        sessionLocked = locked;
    }

    public bool TryStartTechniqueToss()
    {
        if (!CanActivateTechnique)
            return false;

        StartCoroutine(Toss());
        return true;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        restPosition = body.position;
        restRotation = body.rotation;
        restForward = restRotation * Vector3.forward;
        restUp = restRotation * Vector3.up;
    }

    private void Update()
    {
        if (TossKeyPressed() && !tossing && !controlLocked && !sessionLocked)
            StartCoroutine(Toss());
    }

    private IEnumerator Toss()
    {
        tossing = true;
        TossStarted?.Invoke();
        float elapsed = 0f;

        Vector3 pulledBack = restPosition
            - restForward * pullBackDistance
            - restUp * dipDepth;

        Vector3 arcControl = restPosition
            + restForward * 0.04f
            - restUp * dipDepth * 0.28f;

        Vector3 arcTop = restPosition
            + restForward * forwardDistance
            + restUp * arcHeight;

        while (elapsed < tossDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / tossDuration);

            Vector3 targetPosition;
            float pitch;

            if (normalizedTime < anticipationEnd)
            {
                // Pull down and toward the handle to build momentum.
                float phase = Smooth01(normalizedTime / anticipationEnd);
                targetPosition = Vector3.LerpUnclamped(restPosition, pulledBack, phase);
                pitch = Mathf.Lerp(0f, anticipationTilt, phase);
            }
            else if (normalizedTime < swingEnd)
            {
                // Scoop from low/back to high/front along a clear curved path.
                float phase = Smooth01((normalizedTime - anticipationEnd) / (swingEnd - anticipationEnd));
                targetPosition = QuadraticBezier(pulledBack, arcControl, arcTop, phase);
                pitch = Mathf.Lerp(anticipationTilt, -upwardSnapAngle, Smooth01(phase));
            }
            else
            {
                // Settle back without snapping instantly to the start position.
                float phase = Smooth01((normalizedTime - swingEnd) / (1f - swingEnd));
                targetPosition = Vector3.LerpUnclamped(arcTop, restPosition, phase);
                pitch = Mathf.Lerp(-upwardSnapAngle, 0f, phase);
            }

            body.MovePosition(targetPosition);
            body.MoveRotation(restRotation * Quaternion.Euler(pitch, 0f, 0f));
            yield return new WaitForFixedUpdate();
        }

        body.MovePosition(restPosition);
        body.MoveRotation(restRotation);
        tossing = false;
        TossFinished?.Invoke();
    }

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static bool TossKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }
}
