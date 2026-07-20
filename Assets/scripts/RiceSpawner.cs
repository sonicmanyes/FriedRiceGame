using UnityEngine;

public sealed class RiceSpawner : MonoBehaviour
{
    [SerializeField] private Rigidbody ricePrefab;
    [SerializeField, Range(10, 300)] private int amount = 126;
    [SerializeField] private Vector3 spawnArea = new Vector3(0.42f, 0.05f, 0.42f);

    [Header("Ingredient mix")]
    [SerializeField, Range(0f, 1f)] private float eggRatio = 0.20f;
    [SerializeField, Range(0f, 1f)] private float greenOnionRatio = 0.10f;
    [SerializeField] private Color riceColor = new Color(1f, 0.91f, 0.68f);
    [SerializeField] private Color eggColor = new Color(1f, 0.63f, 0.04f);
    [SerializeField] private Color greenOnionColor = new Color(0.15f, 0.65f, 0.12f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Start()
    {
        if (ricePrefab == null)
        {
            Debug.LogError("Rice Prefab is not assigned.", this);
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-spawnArea.x, spawnArea.x),
                Random.Range(0f, spawnArea.y),
                Random.Range(-spawnArea.z, spawnArea.z));

            Rigidbody grain = Instantiate(ricePrefab, transform.position + offset, Random.rotation);
            float roll = Random.value;
            Color color;

            if (roll < greenOnionRatio)
            {
                grain.name = "GreenOnion";
                color = greenOnionColor;
                grain.transform.localScale = Vector3.Scale(
                    grain.transform.localScale, new Vector3(0.85f, 0.55f, 0.85f));
            }
            else if (roll < greenOnionRatio + eggRatio)
            {
                grain.name = "Egg";
                color = eggColor;
                grain.transform.localScale = Vector3.Scale(
                    grain.transform.localScale, new Vector3(1.35f, 0.65f, 1.15f));
            }
            else
            {
                grain.name = "Rice";
                color = riceColor;
            }

            grain.transform.localScale *= Random.Range(0.82f, 1.12f);
            SetColor(grain, color);
        }
    }

    private static void SetColor(Rigidbody ingredient, Color color)
    {
        Renderer renderer = ingredient.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        renderer.SetPropertyBlock(block);
    }
}
