using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DragonRiseTechnique : MonoBehaviour
{
    public bool IsPerforming => performing;
    [SerializeField] private float dragonHeight = 4f;
    [SerializeField] private float riseDuration = 1.15f;
    [SerializeField] private float diveDuration = 0.48f;

    [Header("Technique sound effects")]
    [SerializeField] private AudioClip riseSound;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip impactBoomSound;
    [SerializeField, Range(0f, 1f)] private float riseSoundVolume = 0.72f;
    [SerializeField, Range(0f, 1f)] private float impactSoundVolume = 1.0f;
    [SerializeField, Range(0f, 1f)] private float impactBoomVolume = 0.90f;

    [Header("Impact physical explosion")]
    [SerializeField, Min(0.1f)] private float explosionForce = 5.8f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 0.90f;
    [SerializeField, Range(0f, 2f)] private float explosionUpwardModifier = 0.75f;
    [SerializeField, Min(0.1f)] private float impactAftershockDuration = 0.55f;

    private SlowMotionCommandSystem commandSystem;
    private PanTossController panController;
    private AudioSource techniqueAudio;
    private bool performing;
    private readonly List<RiceFlightData> activeRice = new List<RiceFlightData>();

    private sealed class RiceFlightData
    {
        public Rigidbody body;
        public Vector3 startPosition;
        public Vector3 diveStart;
        public Vector3 landingOffset;
        public float angle;
        public float radius;
        public float heightScale;
        public bool wasKinematic;
        public bool detectedCollisions;
    }

    private void Awake()
    {
        commandSystem = GetComponent<SlowMotionCommandSystem>();
        panController = GetComponent<PanTossController>();
        techniqueAudio = gameObject.AddComponent<AudioSource>();
        techniqueAudio.playOnAwake = false;
        techniqueAudio.spatialBlend = 0f;
        techniqueAudio.dopplerLevel = 0f;
    }

    private void OnEnable()
    {
        if (commandSystem != null) commandSystem.DragonRiseRequested += Perform;
    }

    private void OnDisable()
    {
        if (commandSystem != null) commandSystem.DragonRiseRequested -= Perform;
        if (panController != null) panController.SetControlLocked(false);
        RestoreRicePhysics();
    }

    private void Perform()
    {
        if (!performing) StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        performing = true;
        if (panController != null) panController.SetControlLocked(true);
        if (riseSound != null)
            techniqueAudio.PlayOneShot(riseSound, riseSoundVolume);

        List<Rigidbody> rice = CollectRice();
        PrepareRiceFlight(rice);

        Material flame = CreateFlameMaterial();
        GameObject root = new GameObject("DragonRiseVFX_Runtime");
        GameObject head = CreateDragonHead(root.transform, flame);
        ParticleSystem fire = CreateFire(root.transform, flame, "RisingFlames", true);
        Camera camera = Camera.main;
        Vector3 cameraStartPosition = camera != null ? camera.transform.position : Vector3.zero;
        Quaternion cameraStartRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
        Vector3 cameraForward = cameraStartRotation * Vector3.forward;

        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);
            float eased = t * t * (3f - 2f * t);
            float angle = eased * Mathf.PI * 5f;
            Vector3 center = transform.position + Vector3.up * 0.15f;
            head.transform.position = center + new Vector3(
                Mathf.Cos(angle) * 0.72f, eased * dragonHeight, Mathf.Sin(angle) * 0.72f);
            fire.transform.position = head.transform.position;
            AnimateRiceRise(center, eased);
            UpdateRiseCamera(camera, cameraStartPosition, cameraStartRotation, cameraForward, head.transform.position, eased);
            yield return null;
        }

        yield return new WaitForSeconds(0.16f);
        BeginRiceDive();

        Vector3 diveStart = head.transform.position;
        Vector3 diveCameraStart = camera != null ? camera.transform.position : cameraStartPosition;
        elapsed = 0f;
        while (elapsed < diveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / diveDuration);
            Vector3 target = transform.position + Vector3.up * 0.1f;
            float angle = t * Mathf.PI * 3f;
            Vector3 spiral = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.72f * (1f - t);
            head.transform.position = Vector3.Lerp(diveStart, target, t * t) + spiral;
            fire.transform.position = head.transform.position;
            AnimateRiceDive(t * t);
            UpdateDiveCamera(camera, cameraStartPosition, diveCameraStart, cameraForward,
                head.transform.position, transform.position, t);
            yield return null;
        }

        CreateImpact(root.transform, flame);
        if (impactSound != null)
            techniqueAudio.PlayOneShot(impactSound, impactSoundVolume);
        if (impactBoomSound != null)
            techniqueAudio.PlayOneShot(impactBoomSound, impactBoomVolume);
        ExplodeRiceFromImpact(transform.position + Vector3.down * 0.12f);
        Vector3 impactCameraPosition = camera != null ? camera.transform.position : cameraStartPosition;
        elapsed = 0f;
        while (elapsed < impactAftershockDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / impactAftershockDuration);
            RestoreCameraAfterImpact(camera, cameraStartPosition, cameraStartRotation,
                impactCameraPosition, transform.position, t);
            yield return null;
        }
        if (camera != null)
        {
            camera.transform.position = cameraStartPosition;
            camera.transform.rotation = cameraStartRotation;
        }
        yield return new WaitForSeconds(0.7f);
        Destroy(root);
        Destroy(flame);
        performing = false;
        if (panController != null) panController.SetControlLocked(false);
    }

    private List<Rigidbody> CollectRice()
    {
        RiceGrain[] grains = FindObjectsByType<RiceGrain>(FindObjectsSortMode.None);
        var bodies = new List<Rigidbody>();
        foreach (RiceGrain grain in grains)
        {
            if ((grain.transform.position - transform.position).sqrMagnitude < 16f &&
                grain.TryGetComponent(out Rigidbody body))
                bodies.Add(body);
        }
        return bodies;
    }

    private void PrepareRiceFlight(List<Rigidbody> bodies)
    {
        activeRice.Clear();
        for (int i = 0; i < bodies.Count; i++)
        {
            Rigidbody body = bodies[i];
            Vector2 landing = Random.insideUnitCircle * 0.16f;
            var data = new RiceFlightData
            {
                body = body,
                startPosition = body.position,
                landingOffset = new Vector3(landing.x, 0.20f + Random.Range(0f, 0.10f), landing.y),
                angle = Random.Range(0f, Mathf.PI * 2f),
                radius = Random.Range(0.12f, 0.68f),
                heightScale = Random.Range(0.72f, 1.02f),
                wasKinematic = body.isKinematic,
                detectedCollisions = body.detectCollisions
            };
            body.isKinematic = true;
            body.detectCollisions = false;
            activeRice.Add(data);
        }
    }

    private void AnimateRiceRise(Vector3 center, float progress)
    {
        float turns = progress * Mathf.PI * 5.5f;
        foreach (RiceFlightData data in activeRice)
        {
            float angle = data.angle + turns;
            Vector3 helix = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * data.radius;
            Vector3 flightPosition = center + helix + Vector3.up * (dragonHeight * progress * data.heightScale);
            data.body.position = Vector3.Lerp(data.startPosition, flightPosition, progress);
            data.body.rotation = Quaternion.Euler(angle * Mathf.Rad2Deg, angle * 43f, angle * 19f);
        }
    }

    private void BeginRiceDive()
    {
        foreach (RiceFlightData data in activeRice)
            data.diveStart = data.body.position;
    }

    private void AnimateRiceDive(float progress)
    {
        foreach (RiceFlightData data in activeRice)
        {
            Vector3 landingPosition = transform.TransformPoint(data.landingOffset);
            data.body.position = Vector3.Lerp(data.diveStart, landingPosition, progress);
            data.body.transform.Rotate(Vector3.one, 720f * Time.deltaTime, Space.World);
        }
    }

    private void ExplodeRiceFromImpact(Vector3 explosionPoint)
    {
        foreach (RiceFlightData data in activeRice)
        {
            if (data.body == null) continue;
            data.body.detectCollisions = data.detectedCollisions;
            data.body.isKinematic = data.wasKinematic;
            if (data.wasKinematic) continue;

            SetVelocity(data.body, Vector3.zero);
            data.body.AddExplosionForce(
                explosionForce,
                explosionPoint,
                explosionRadius,
                explosionUpwardModifier,
                ForceMode.VelocityChange);
            data.body.angularVelocity = Random.insideUnitSphere * 14f;
        }
        activeRice.Clear();
    }

    private void RestoreRicePhysics()
    {
        foreach (RiceFlightData data in activeRice)
        {
            if (data.body == null) continue;
            data.body.detectCollisions = data.detectedCollisions;
            data.body.isKinematic = data.wasKinematic;
            if (!data.wasKinematic)
            {
                SetVelocity(data.body, Vector3.down * 0.45f + Random.insideUnitSphere * 0.18f);
                data.body.angularVelocity = Random.insideUnitSphere * 7f;
            }
        }
        activeRice.Clear();
    }

    private GameObject CreateDragonHead(Transform parent, Material material)
    {
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "FlameDragonHead";
        head.transform.SetParent(parent);
        head.transform.localScale = new Vector3(0.45f, 0.3f, 0.62f);
        Destroy(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = material;
        TrailRenderer trail = head.AddComponent<TrailRenderer>();
        trail.time = 0.8f;
        trail.startWidth = 0.38f;
        trail.endWidth = 0.02f;
        trail.numCornerVertices = 5;
        trail.sharedMaterial = material;
        trail.startColor = Color.yellow;
        trail.endColor = new Color(1f, 0f, 0f, 0f);
        return head;
    }

    private ParticleSystem CreateFire(Transform parent, Material material, string name, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = loop;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.yellow, new Color(1f, 0.05f, 0f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = loop ? 110f : 0f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;
        system.GetComponent<ParticleSystemRenderer>().material = material;
        system.Play();
        return system;
    }

    private void CreateImpact(Transform parent, Material material)
    {
        ParticleSystem burst = CreateFire(parent, material, "ImpactFlameBurst", false);
        burst.transform.position = transform.position + Vector3.up * 0.12f;
        ParticleSystem.MainModule main = burst.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
        ParticleSystem.EmissionModule emission = burst.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });
        ParticleSystem.ShapeModule shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.25f;
        burst.Play();
    }

    private static Material CreateFlameMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material material = new Material(shader);
        material.color = new Color(1f, 0.18f, 0.01f);
        return material;
    }

    private static void UpdateRiseCamera(Camera camera, Vector3 startPosition,
        Quaternion startRotation, Vector3 startForward, Vector3 dragonPosition, float progress)
    {
        if (camera == null) return;
        float movement = SmoothStep(progress);
        camera.transform.position = Vector3.Lerp(
            startPosition,
            startPosition + startForward * 0.42f + Vector3.up * 0.32f,
            movement);
        Quaternion lookRotation = Quaternion.LookRotation(dragonPosition - camera.transform.position, Vector3.up);
        camera.transform.rotation = Quaternion.Slerp(startRotation, lookRotation, movement);
    }

    private static void UpdateDiveCamera(Camera camera, Vector3 originalPosition,
        Vector3 diveStartPosition, Vector3 startForward, Vector3 dragonPosition,
        Vector3 panPosition, float progress)
    {
        if (camera == null) return;
        float movement = SmoothStep(progress);
        Vector3 closePosition = originalPosition + startForward * 1.05f + Vector3.up * 0.12f;
        camera.transform.position = Vector3.Lerp(diveStartPosition, closePosition, movement)
            + Random.insideUnitSphere * (progress * 0.025f);
        Vector3 lookTarget = Vector3.Lerp(dragonPosition, panPosition + Vector3.up * 0.2f, progress * 0.55f);
        camera.transform.rotation = Quaternion.LookRotation(lookTarget - camera.transform.position, Vector3.up);
    }

    private static void RestoreCameraAfterImpact(Camera camera, Vector3 originalPosition,
        Quaternion originalRotation, Vector3 impactPosition, Vector3 panPosition, float progress)
    {
        if (camera == null) return;
        float movement = SmoothStep(progress);
        float shake = (1f - progress) * 0.075f;
        camera.transform.position = Vector3.Lerp(impactPosition, originalPosition, movement)
            + Random.insideUnitSphere * shake;
        Quaternion impactLook = Quaternion.LookRotation(
            panPosition + Vector3.up * 0.15f - camera.transform.position, Vector3.up);
        camera.transform.rotation = Quaternion.Slerp(impactLook, originalRotation, movement);
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
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
