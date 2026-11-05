using UnityEngine;
using UnityEngine.InputSystem;

namespace CityParking
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class ParkingCar : MonoBehaviour
    {
        public bool driving = true;
        public ParkingCourse course;
        public ParkingMobileHUD mobileControls;
        Rigidbody body;
        float throttle, steer;
        bool brake;
        public float Speed => body == null ? 0 : body.linearVelocity.magnitude;
        void Awake() { body = GetComponent<Rigidbody>(); }
        void Update()
        {
            var k = Keyboard.current;
            throttle = driving && k != null ? ((k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0)) : 0;
            steer = driving && k != null ? ((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0)) : 0;
            bool blocked = !driving || (course != null && course.IsPaused);
            if (mobileControls != null && !blocked)
            {
                if (mobileControls.Throttle != 0) throttle = mobileControls.Throttle;
                if (mobileControls.Steering != 0) steer = mobileControls.Steering;
            }
            if (blocked) throttle = steer = 0;
            brake = blocked || (k != null && k.spaceKey.isPressed) || (mobileControls != null && mobileControls.Braking);
        }
        void FixedUpdate()
        {
            if (course != null && course.IsPaused)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                return;
            }
            float speed = Vector3.Dot(body.linearVelocity, transform.forward);
            float target = brake ? 0 : throttle * (throttle < 0 ? 3 : 5);
            speed = Mathf.MoveTowards(speed, target, (brake ? 12 : throttle == 0 ? 2.5f : 4) * Time.fixedDeltaTime);
            body.linearVelocity = transform.forward * speed;
            body.MoveRotation(body.rotation * Quaternion.Euler(0, steer * speed * 17 * Time.fixedDeltaTime, 0));
        }
        public void ResetAt(Transform spawn)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = spawn.position;
            body.rotation = spawn.rotation;
            transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            throttle = steer = 0;
            mobileControls?.ClearControls();
            driving = true;
        }
        void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.CompareTag("Respawn")) course?.RegisterCollision();
        }
    }
}
