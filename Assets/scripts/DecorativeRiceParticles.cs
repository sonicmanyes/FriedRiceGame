using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class DecorativeRiceParticles : MonoBehaviour
{
    [SerializeField, Range(50, 600)] private int visualRiceAmount = 260;
    [SerializeField, Min(0.1f)] private float spreadRadius = 0.60f;
    [SerializeField] private Color riceColor = new Color(1f, 0.90f, 0.62f, 1f);

    private Material runtimeMaterial;
    private ParticleSystem particles;
    private PanTossController panController;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();
        panController = GetComponentInParent<PanTossController>();
        if (panController != null)
        {
            panController.TossStarted += HideDuringToss;
            panController.TossFinished += ShowAfterToss;
        }

        Configure();
    }

    private void Configure()
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 9999f;
        main.startSpeed = 0f;
        main.startColor = riceColor;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.014f, 0.021f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.034f, 0.048f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.014f, 0.021f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.maxParticles = visualRiceAmount + 20;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)visualRiceAmount)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = spreadRadius;
        shape.radiusThickness = 1f;
        shape.position = new Vector3(0f, 0.09f, 0f);
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        particleRenderer.enableGPUInstancing = true;
        particleRenderer.sortMode = ParticleSystemSortMode.None;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader != null)
        {
            runtimeMaterial = new Material(shader) { name = "Decorative Rice Runtime Material" };
            runtimeMaterial.color = riceColor;
            particleRenderer.material = runtimeMaterial;
        }

        particles.Play();
    }

    private void OnDestroy()
    {
        if (panController != null)
        {
            panController.TossStarted -= HideDuringToss;
            panController.TossFinished -= ShowAfterToss;
        }

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void HideDuringToss()
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ShowAfterToss()
    {
        particles.Play(true);
    }
}
