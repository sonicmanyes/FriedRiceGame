using UnityEngine;

[DisallowMultipleComponent]
public sealed class RiceGuideTube : MonoBehaviour
{
    [Header("Invisible safety tube")]
    [SerializeField, Range(12, 40)] private int segments = 24;
    [SerializeField, Min(0.2f)] private float innerRadius = 0.70f;
    [SerializeField, Min(0.2f)] private float height = 2.2f;
    [SerializeField, Min(0.01f)] private float thickness = 0.06f;
    [SerializeField, Min(0f)] private float bottomOffset = 0.02f;
    [SerializeField, Min(0.02f)] private float bottomThickness = 0.14f;

    [Header("Handle-side guard")]
    [SerializeField, Min(0.2f)] private float rearGuardWidth = 1.45f;
    [SerializeField, Min(0.1f)] private float rearGuardHeight = 0.72f;
    [SerializeField, Min(0.02f)] private float rearGuardThickness = 0.10f;

    private const string GuideRootName = "RiceGuideTube_Runtime";

    private void Awake()
    {
        BuildTube();
    }

    private void BuildTube()
    {
        Transform oldGuide = transform.Find(GuideRootName);
        if (oldGuide != null)
            Destroy(oldGuide.gameObject);

        // Return to the original behaviour: the entire invisible container is
        // a child of PanRoot and follows both its position and rotation.
        GameObject guideRoot = new GameObject(GuideRootName);
        guideRoot.transform.SetParent(transform, false);

        GameObject floor = new GameObject("GuideBottom");
        floor.transform.SetParent(guideRoot.transform, false);
        floor.transform.localPosition = new Vector3(0f, -bottomThickness * 0.5f + 0.035f, 0f);
        BoxCollider floorCollider = floor.AddComponent<BoxCollider>();
        floorCollider.size = new Vector3(1.42f, bottomThickness, 1.42f);

        CreateRearGuard(guideRoot.transform, -innerRadius - 0.09f);

        float circumference = 2f * Mathf.PI * innerRadius;
        float segmentWidth = circumference / segments * 1.08f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            GameObject wall = new GameObject("Guide_" + i.ToString("00"));
            wall.transform.SetParent(guideRoot.transform, false);
            wall.transform.localPosition = new Vector3(
                Mathf.Sin(angle) * innerRadius,
                bottomOffset + height * 0.5f,
                Mathf.Cos(angle) * innerRadius);
            wall.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);

            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(segmentWidth, height, thickness);
        }
    }

    private void CreateRearGuard(Transform parent, float zPosition)
    {
        GameObject guard = new GameObject("HandleSideGuard");
        guard.transform.SetParent(parent, false);
        guard.transform.localPosition = new Vector3(
            0f,
            bottomOffset + rearGuardHeight * 0.5f,
            zPosition);

        BoxCollider guardCollider = guard.AddComponent<BoxCollider>();
        guardCollider.size = new Vector3(rearGuardWidth, rearGuardHeight, rearGuardThickness);
    }
}
