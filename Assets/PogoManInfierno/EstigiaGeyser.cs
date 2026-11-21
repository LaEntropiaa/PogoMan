using UnityEngine;
using UnityEngine.Events;

namespace PogoMan.Estigia
{
    public class EstigiaGeyser : MonoBehaviour
    {
        public Transform landing;
        public string playerTag = "Player";
        public float clearance = 2.2f;
        [System.Serializable] public class LaunchEvent : UnityEvent<Vector3> {}
        public LaunchEvent onLaunch = new LaunchEvent();
        private float nextLaunch;

        private void OnTriggerEnter(Collider other)
        {
            if (!landing || Time.time < nextLaunch) return;
            var body = other.attachedRigidbody;
            if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag)
                && !(body && body.CompareTag(playerTag))) return;
            var origin = body ? body.worldCenterOfMass : other.bounds.center;
            float feet = other.bounds.min.y;
            var offset = landing.position - origin;
            float dy = landing.position.y - feet + .1f;
            float gravity = Mathf.Max(.1f, -Physics.gravity.y);
            float vertical = Mathf.Sqrt(2f * gravity * (Mathf.Max(0f, dy) + clearance));
            float time = (vertical + Mathf.Sqrt(Mathf.Max(0f, vertical * vertical - 2f * gravity * dy))) / gravity;
            var velocity = new Vector3(offset.x / time, vertical, offset.z / time);
            nextLaunch = Time.time + .7f;
            if (body && !body.isKinematic)
                body.AddForce(velocity - body.GetPointVelocity(body.worldCenterOfMass), ForceMode.VelocityChange);
            onLaunch.Invoke(velocity);
        }
    }
}
