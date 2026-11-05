using UnityEngine;

namespace CityParking
{
    [RequireComponent(typeof(Camera))]
    public class ParkingCamera : MonoBehaviour
    {
        public Transform target;
        [Range(10, 24)] public float drivingSize = 14;
        public float followSharpness = 7;
        [Min(.1f)] public float introDuration = 2;
        public const float MinZoomSize = 10, MaxZoomSize = 24;
        public const float IsometricPitch = 35.2643897f;
        public Quaternion ViewRotation => Quaternion.Euler(IsometricPitch, 45, 0);
        public bool IntroPlaying { get; private set; }
        public float Zoom01 => Mathf.InverseLerp(MaxZoomSize, MinZoomSize, drivingSize);
        Camera view;
        Vector3 focus;
        float introElapsed;
        void Awake() { view = GetComponent<Camera>(); view.orthographic = true; }
        public void BeginLevel()
        {
            introElapsed = 0;
            focus = Vector3.zero;
            IntroPlaying = true;
        }
        public void SetZoom(float value) { drivingSize = Mathf.Lerp(MaxZoomSize, MinZoomSize, Mathf.Clamp01(value)); }
        public static float CourseSize(Quaternion rotation, float aspect)
        {
            float x = 0, y = 0;
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    Vector3 p = Quaternion.Inverse(rotation) * new Vector3(a * 18, 0, b * 23);
                    x = Mathf.Max(x, Mathf.Abs(p.x)); y = Mathf.Max(y, Mathf.Abs(p.y));
                }
            return Mathf.Max(y / .54f, x / (Mathf.Max(.1f, aspect) * .88f)) + 1;
        }
        void LateUpdate()
        {
            if (!target) return;
            Rect safe = Screen.safeArea;
            if (safe.width > 0 && safe.height > 0)
                view.rect = new Rect(safe.x / Screen.width, safe.y / Screen.height, safe.width / Screen.width, safe.height / Screen.height);
            Quaternion rotation = ViewRotation;
            Vector3 desired = target.position + Vector3.up * .5f;
            // Preserve horizontal driving visibility on tall portrait phones.
            float size = drivingSize * Mathf.Max(1, (9f / 16) / Mathf.Max(.1f, view.aspect));
            if (IntroPlaying)
            {
                float t = Mathf.Clamp01(introElapsed / Mathf.Max(.1f, introDuration));
                float eased = t * t * (3 - 2 * t);
                focus = Vector3.Lerp(Vector3.zero, desired, eased);
                view.orthographicSize = Mathf.Lerp(Mathf.Max(size * 1.6f, CourseSize(rotation, view.aspect)), size, eased);
                introElapsed += Time.unscaledDeltaTime;
                if (t >= 1) IntroPlaying = false;
            }
            else
            {
                float blend = 1 - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
                focus = Vector3.Lerp(focus, desired, blend);
                view.orthographicSize = Mathf.Lerp(view.orthographicSize, size, blend);
            }
            Vector3 aim = focus - rotation * Vector3.up * (view.orthographicSize * .10f);
            transform.SetPositionAndRotation(aim - rotation * Vector3.forward * 90, rotation);
        }
    }
}
