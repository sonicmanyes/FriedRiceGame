using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class TornadoSpinTechnique : MonoBehaviour
{
    public bool IsPerforming => performing;

    [Header("Mash timing")]
    [SerializeField, Min(0.5f)] private float mashDuration = 3.5f;
    [SerializeField, Min(0f)] private float mashInputDelay = 0.30f;
    [SerializeField, Min(1)] private int stageTwoMashes = 6;
    [SerializeField, Min(2)] private int stageThreeMashes = 12;
    [SerializeField, Min(3)] private int maximumScoringMashes = 18;

    [Header("Mash score")]
    [SerializeField, Min(0)] private int stageOneScorePerMash = 30;
    [SerializeField, Min(0)] private int stageTwoScorePerMash = 50;
    [SerializeField, Min(0)] private int stageThreeScorePerMash = 80;

    [Header("Tornado appearance")]
    [SerializeField] private Color tornadoColor = new Color(0.16f, 1f, 0.28f, 1f);
    [SerializeField, Min(0.1f)] private float stageOneHeight = 1.15f;
    [SerializeField, Min(0.1f)] private float stageTwoHeight = 2.05f;
    [SerializeField, Min(0.1f)] private float stageThreeHeight = 3.65f;

    private sealed class IngredientFlight
    {
        public Rigidbody body;
        public Vector3 startPosition;
        public Vector3 finishStart;
        public Vector3 landingOffset;
        public float angle;
        public float heightRatio;
        public bool wasKinematic;
        public bool detectedCollisions;
    }

    private readonly List<IngredientFlight> ingredients = new List<IngredientFlight>();
    private readonly List<TrailRenderer> spiralTrails = new List<TrailRenderer>();
    private SlowMotionCommandSystem commandSystem;
    private PanTossController panController;
    private AudioSource windAudio;
    private AudioClip[] stageWhooshes;
    private ParticleSystem windParticles;
    private Material windMaterial;
    private Camera mainCamera;
    private Vector3 cameraStartPosition;
    private Quaternion cameraStartRotation;
    private bool performing;
    private int mashCount;
    private int windStage;
    private GameObject windGaugeRoot;
    private RectTransform windGaugeFillRect;
    private Text windGaugeText;

    private void Awake()
    {
        commandSystem = GetComponent<SlowMotionCommandSystem>();
        panController = GetComponent<PanTossController>();
        windAudio = gameObject.AddComponent<AudioSource>();
        windAudio.playOnAwake = false;
        windAudio.spatialBlend = 0f;
        windAudio.dopplerLevel = 0f;
        stageWhooshes = new[]
        {
            CreateWhooshClip("Tornado Stage 1", 0.42f, 0.78f, 4101),
            CreateWhooshClip("Tornado Stage 2", 0.58f, 1.00f, 4102),
            CreateWhooshClip("Tornado Stage 3", 0.82f, 1.28f, 4103)
        };
        BuildWindGaugeUi();
    }

    private void OnEnable()
    {
        if (commandSystem != null)
            commandSystem.TornadoSpinRequested += Perform;
    }

    private void OnDisable()
    {
        if (commandSystem != null)
            commandSystem.TornadoSpinRequested -= Perform;
        if (panController != null)
            panController.SetControlLocked(false);
        if (windGaugeRoot != null)
            windGaugeRoot.SetActive(false);
        RestoreIngredientPhysics();
        RestoreCamera();
    }

    private void OnDestroy()
    {
        if (stageWhooshes == null)
            return;

        foreach (AudioClip clip in stageWhooshes)
            if (clip != null) Destroy(clip);
    }

    private void Perform()
    {
        if (!performing)
            StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        performing = true;
        mashCount = 0;
        windStage = 0;
        RefreshWindGauge();
        windGaugeRoot.SetActive(true);
        if (panController != null)
            panController.SetControlLocked(true);

        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraStartPosition = mainCamera.transform.position;
            cameraStartRotation = mainCamera.transform.rotation;
        }

        PrepareIngredients();
        GameObject vfxRoot = BuildTornadoVfx();

        float elapsed = 0f;
        while (elapsed < mashDuration)
        {
            elapsed += Time.deltaTime;
            bool acceptingMash = elapsed >= mashInputDelay;
            if (acceptingMash && AKeyPressed())
                RegisterMash();

            float visualStrength = CurrentVisualStrength();
            AnimateTornado(elapsed, visualStrength);
            AnimateIngredients(elapsed, visualStrength);
            UpdateCamera(elapsed, visualStrength);
            yield return null;
        }

        CacheFinishPositions();
        if (windStage >= 3)
            yield return BlowUpAndRainDown();
        else
            yield return CollapseIntoPan();

        RestoreIngredientPhysics();
        RestoreCamera();
        if (vfxRoot != null) Destroy(vfxRoot);
        if (windMaterial != null) Destroy(windMaterial);
        windMaterial = null;
        windGaugeRoot.SetActive(false);
        performing = false;
        if (panController != null)
            panController.SetControlLocked(false);
    }

    private void RegisterMash()
    {
        mashCount++;
        int nextStage = mashCount >= stageThreeMashes ? 3 : mashCount >= stageTwoMashes ? 2 : 1;
        if (nextStage != windStage)
        {
            windStage = nextStage;
            PlayStageWhoosh(windStage);
            BurstWindParticles(windStage);
        }

        RefreshWindGauge();

        if (mashCount <= maximumScoringMashes && commandSystem != null)
        {
            int points = windStage == 1
                ? stageOneScorePerMash
                : windStage == 2 ? stageTwoScorePerMash : stageThreeScorePerMash;
            commandSystem.AddTechniqueScore(points);
        }
    }

    private float CurrentVisualStrength()
    {
        if (windStage <= 0) return 0.18f;
        if (windStage == 1)
            return Mathf.Lerp(0.28f, 0.48f, Mathf.InverseLerp(1f, stageTwoMashes, mashCount));
        if (windStage == 2)
            return Mathf.Lerp(0.55f, 0.78f, Mathf.InverseLerp(stageTwoMashes, stageThreeMashes, mashCount));
        return Mathf.Lerp(0.86f, 1f, Mathf.InverseLerp(stageThreeMashes, maximumScoringMashes, mashCount));
    }

    private void PrepareIngredients()
    {
        ingredients.Clear();
        RiceGrain[] grains = FindObjectsByType<RiceGrain>(FindObjectsSortMode.None);
        for (int i = 0; i < grains.Length; i++)
        {
            RiceGrain grain = grains[i];
            if ((grain.transform.position - transform.position).sqrMagnitude >= 16f ||
                !grain.TryGetComponent(out Rigidbody body))
                continue;

            Vector2 landing = Random.insideUnitCircle * 0.42f;
            ingredients.Add(new IngredientFlight
            {
                body = body,
                startPosition = body.position,
                landingOffset = new Vector3(landing.x, 0.16f + Random.Range(0f, 0.08f), landing.y),
                angle = Random.Range(0f, Mathf.PI * 2f),
                heightRatio = (i + Random.value) / Mathf.Max(1f, grains.Length),
                wasKinematic = body.isKinematic,
                detectedCollisions = body.detectCollisions
            });
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private GameObject BuildTornadoVfx()
    {
        GameObject root = new GameObject("TornadoSpinVFX_Runtime");
        root.transform.position = transform.position + Vector3.up * 0.12f;
        windMaterial = CreateWindMaterial();

        spiralTrails.Clear();
        for (int i = 0; i < 7; i++)
        {
            GameObject arm = new GameObject("GreenWindArm_" + i);
            arm.transform.SetParent(root.transform, false);
            TrailRenderer trail = arm.AddComponent<TrailRenderer>();
            trail.time = 0.55f;
            trail.minVertexDistance = 0.025f;
            trail.startWidth = 0.11f;
            trail.endWidth = 0.015f;
            trail.numCornerVertices = 4;
            trail.sharedMaterial = windMaterial;
            trail.startColor = tornadoColor;
            trail.endColor = new Color(tornadoColor.r, tornadoColor.g, tornadoColor.b, 0f);
            spiralTrails.Add(trail);
        }

        GameObject particleObject = new GameObject("GreenWindParticles");
        particleObject.transform.SetParent(root.transform, false);
        windParticles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = windParticles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(tornadoColor, new Color(0.75f, 1f, 0.3f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 420;
        ParticleSystem.EmissionModule emission = windParticles.emission;
        emission.rateOverTime = 55f;
        ParticleSystem.ShapeModule shape = windParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.48f;
        ParticleSystemRenderer renderer = windParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = windMaterial;
        windParticles.Play();
        return root;
    }

    private void AnimateTornado(float elapsed, float strength)
    {
        float height = Mathf.Lerp(stageOneHeight, stageThreeHeight, strength);
        float radius = Mathf.Lerp(0.38f, 1.28f, strength);
        float rotationSpeed = Mathf.Lerp(4.5f, 11.5f, strength);
        for (int i = 0; i < spiralTrails.Count; i++)
        {
            TrailRenderer trail = spiralTrails[i];
            if (trail == null) continue;
            float phase = elapsed * rotationSpeed + i * Mathf.PI * 2f / spiralTrails.Count;
            float verticalCycle = Mathf.Repeat(elapsed * (0.52f + strength * 0.35f) + i * 0.19f, 1f);
            float armRadius = radius * Mathf.Lerp(0.34f, 1f, verticalCycle);
            trail.transform.position = transform.position + Vector3.up * (0.15f + height * verticalCycle)
                + new Vector3(Mathf.Cos(phase) * armRadius, 0f, Mathf.Sin(phase) * armRadius);
            trail.startWidth = Mathf.Lerp(0.07f, 0.22f, strength);
            trail.time = Mathf.Lerp(0.35f, 0.78f, strength);
        }

        if (windParticles != null)
        {
            ParticleSystem.EmissionModule emission = windParticles.emission;
            emission.rateOverTime = Mathf.Lerp(55f, 220f, strength);
            ParticleSystem.ShapeModule shape = windParticles.shape;
            shape.radius = radius * 0.72f;
            ParticleSystem.MainModule main = windParticles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f + strength, 2.2f + strength * 3.2f);
        }
    }

    private void AnimateIngredients(float elapsed, float strength)
    {
        float height = Mathf.Lerp(stageOneHeight * 0.72f, stageThreeHeight * 0.86f, strength);
        float outerRadius = Mathf.Lerp(0.44f, 1.18f, strength);
        float speed = Mathf.Lerp(4.5f, 12f, strength);
        Vector3 center = transform.position + Vector3.up * 0.12f;
        foreach (IngredientFlight data in ingredients)
        {
            if (data.body == null) continue;
            float vertical = Mathf.Repeat(data.heightRatio + elapsed * (0.12f + strength * 0.12f), 1f);
            float radius = Mathf.Lerp(0.18f, outerRadius, vertical);
            float angle = data.angle + elapsed * speed + vertical * Mathf.PI * 7f;
            Vector3 spiral = new Vector3(Mathf.Cos(angle) * radius, vertical * height, Mathf.Sin(angle) * radius);
            Vector3 target = center + spiral;
            data.body.position = Vector3.Lerp(data.body.position, target, Mathf.Clamp01(Time.deltaTime * 9f));
            data.body.rotation = Quaternion.Euler(angle * Mathf.Rad2Deg, elapsed * 540f, vertical * 360f);
        }
    }

    private void UpdateCamera(float elapsed, float strength)
    {
        if (mainCamera == null) return;
        float pullBack = Mathf.Lerp(0f, 0.52f, strength);
        Vector3 backward = -(cameraStartRotation * Vector3.forward) * pullBack;
        float shake = windStage >= 3 ? 0.025f + Mathf.Sin(elapsed * 38f) * 0.012f : 0f;
        mainCamera.transform.position = cameraStartPosition + backward + Random.insideUnitSphere * shake;
        Vector3 lookTarget = transform.position + Vector3.up * Mathf.Lerp(0.35f, 1.25f, strength);
        mainCamera.transform.rotation = Quaternion.Slerp(
            cameraStartRotation,
            Quaternion.LookRotation(lookTarget - mainCamera.transform.position, Vector3.up),
            strength * 0.42f);
    }

    private void CacheFinishPositions()
    {
        foreach (IngredientFlight data in ingredients)
            if (data.body != null) data.finishStart = data.body.position;
    }

    private IEnumerator BlowUpAndRainDown()
    {
        const float riseTime = 0.58f;
        const float fallTime = 0.82f;
        float elapsed = 0f;
        while (elapsed < riseTime)
        {
            elapsed += Time.deltaTime;
            float t = Smooth01(elapsed / riseTime);
            foreach (IngredientFlight data in ingredients)
            {
                if (data.body == null) continue;
                Vector3 burst = transform.position + Vector3.up * (3.4f + data.heightRatio * 1.1f);
                burst += new Vector3(Mathf.Cos(data.angle), 0f, Mathf.Sin(data.angle)) * (0.25f + data.heightRatio * 0.5f);
                data.body.position = Vector3.Lerp(data.finishStart, burst, t);
            }
            yield return null;
        }

        CacheFinishPositions();
        yield return new WaitForSeconds(0.10f);
        elapsed = 0f;
        while (elapsed < fallTime)
        {
            elapsed += Time.deltaTime;
            float t = Smooth01(elapsed / fallTime);
            foreach (IngredientFlight data in ingredients)
            {
                if (data.body == null) continue;
                Vector3 landing = transform.TransformPoint(data.landingOffset);
                data.body.position = Vector3.Lerp(data.finishStart, landing, t);
                data.body.transform.Rotate(Vector3.one, 720f * Time.deltaTime, Space.World);
            }
            yield return null;
        }
    }

    private IEnumerator CollapseIntoPan()
    {
        const float collapseTime = 0.58f;
        float elapsed = 0f;
        while (elapsed < collapseTime)
        {
            elapsed += Time.deltaTime;
            float t = Smooth01(elapsed / collapseTime);
            foreach (IngredientFlight data in ingredients)
            {
                if (data.body == null) continue;
                Vector3 landing = transform.TransformPoint(data.landingOffset);
                data.body.position = Vector3.Lerp(data.finishStart, landing, t);
            }
            yield return null;
        }
    }

    private void RestoreIngredientPhysics()
    {
        foreach (IngredientFlight data in ingredients)
        {
            if (data.body == null) continue;
            data.body.detectCollisions = data.detectedCollisions;
            data.body.isKinematic = data.wasKinematic;
            if (!data.wasKinematic)
            {
                SetVelocity(data.body, Vector3.down * 0.25f);
                data.body.angularVelocity = Random.insideUnitSphere * 5f;
            }
        }
        ingredients.Clear();
    }

    private void PlayStageWhoosh(int stage)
    {
        if (stageWhooshes == null || stage < 1 || stage > stageWhooshes.Length)
            return;
        float volume = stage == 1 ? 0.34f : stage == 2 ? 0.68f : 1f;
        windAudio.PlayOneShot(stageWhooshes[stage - 1], volume);
    }

    private void BurstWindParticles(int stage)
    {
        if (windParticles == null) return;
        windParticles.Emit(stage == 1 ? 35 : stage == 2 ? 75 : 140);
    }

    private void BuildWindGaugeUi()
    {
        windGaugeRoot = new GameObject("TornadoWindGaugeUI");
        windGaugeRoot.transform.SetParent(transform, false);
        Canvas canvas = windGaugeRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 170;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = windGaugeRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject("WindGaugeBackground");
        backgroundObject.transform.SetParent(windGaugeRoot.transform, false);
        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.23f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.23f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(500f, 62f);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0.01f, 0.035f, 0.015f, 0.92f);

        GameObject interiorObject = new GameObject("WindGaugeInterior");
        interiorObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform interiorRect = interiorObject.AddComponent<RectTransform>();
        interiorRect.anchorMin = new Vector2(0.025f, 0.18f);
        interiorRect.anchorMax = new Vector2(0.975f, 0.82f);
        interiorRect.offsetMin = Vector2.zero;
        interiorRect.offsetMax = Vector2.zero;
        Image interior = interiorObject.AddComponent<Image>();
        interior.color = new Color(0.08f, 0.12f, 0.08f, 1f);

        GameObject fillObject = new GameObject("WindGaugeGreenFill");
        fillObject.transform.SetParent(interiorObject.transform, false);
        windGaugeFillRect = fillObject.AddComponent<RectTransform>();
        windGaugeFillRect.anchorMin = Vector2.zero;
        windGaugeFillRect.anchorMax = new Vector2(0f, 1f);
        windGaugeFillRect.pivot = new Vector2(0f, 0.5f);
        windGaugeFillRect.offsetMin = Vector2.zero;
        windGaugeFillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.AddComponent<Image>();
        fill.color = new Color(0.12f, 1f, 0.24f, 1f);

        GameObject textObject = new GameObject("WindLevelText");
        textObject.transform.SetParent(windGaugeRoot.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.305f);
        textRect.anchorMax = new Vector2(0.5f, 0.305f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(600f, 54f);
        windGaugeText = textObject.AddComponent<Text>();
        windGaugeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        windGaugeText.fontSize = 34;
        windGaugeText.fontStyle = FontStyle.Bold;
        windGaugeText.alignment = TextAnchor.MiddleCenter;
        windGaugeText.color = Color.white;
        windGaugeText.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        windGaugeRoot.SetActive(false);
    }

    private void RefreshWindGauge()
    {
        if (windGaugeFillRect == null || windGaugeText == null)
            return;

        int shownLevel = mashCount >= stageThreeMashes ? 3 : mashCount >= stageTwoMashes ? 2 : 1;
        float ratio = Mathf.Clamp01(mashCount / (float)Mathf.Max(1, stageThreeMashes));
        windGaugeFillRect.anchorMax = new Vector2(ratio, 1f);
        windGaugeFillRect.offsetMin = Vector2.zero;
        windGaugeFillRect.offsetMax = Vector2.zero;
        windGaugeText.text = "WIND LEVEL " + shownLevel + "     A  MASH!";
        windGaugeText.color = shownLevel == 3
            ? new Color(0.75f, 1f, 0.25f)
            : Color.white;
    }

    private void RestoreCamera()
    {
        if (mainCamera == null) return;
        mainCamera.transform.position = cameraStartPosition;
        mainCamera.transform.rotation = cameraStartRotation;
    }

    private Material CreateWindMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material material = new Material(shader) { name = "Tornado Green Wind Runtime Material" };
        material.color = tornadoColor;
        return material;
    }

    private static AudioClip CreateWhooshClip(string name, float duration, float pitchScale, int seed)
    {
        const int sampleRate = 44100;
        int count = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[count];
        var random = new System.Random(seed);
        float filtered = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float normalized = t / duration;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, noise, 0.035f + normalized * 0.09f);
            float envelope = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
            float sweep = Mathf.Sin(2f * Mathf.PI * (130f + 520f * normalized) * pitchScale * t);
            samples[i] = Mathf.Clamp((filtered * 0.72f + sweep * 0.16f) * envelope, -0.95f, 0.95f);
        }
        AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static bool AKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.A);
#endif
    }

    private static void SetVelocity(Rigidbody body, Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = velocity;
#else
        body.velocity = velocity;
#endif
    }
}
