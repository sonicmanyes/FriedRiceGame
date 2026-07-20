using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public sealed class RiceGrain : MonoBehaviour
{
    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.mass = 0.002f;
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = 0.08f;
        body.angularDamping = 0.15f;
#else
        body.drag = 0.08f;
        body.angularDrag = 0.15f;
#endif
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.maxAngularVelocity = 20f;
    }
}
