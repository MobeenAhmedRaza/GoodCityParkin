using UnityEngine;
using UnityEngine.InputSystem;
namespace CityParking
{
    public class ParkingCourse : MonoBehaviour
    {
        [HideInInspector] public ParkingLevel[] levels; // Legacy scene migration only.
        public ParkingLevelCatalog catalog;
        ParkingLevel activeLevel;
        const string LastPlayedKey = "CityParking.LastPlayedLevel";
        public int LevelCount => catalog != null ? catalog.levelCount : levels.Length;
        public string LevelTitle(int index) => catalog != null ? catalog.Title(index) : levels[index].title;
        public ParkingCar car;
        public Camera overview;
        public int startingLevel;
        int current, hits;
        float remaining, hold;
        bool finished, passed, reversedIntoBay;
        string status = "";
        bool menuOpen, suspended;
        public bool IsPaused => menuOpen || suspended || (overview != null && overview.GetComponent<ParkingCamera>() is ParkingCamera rig && rig.IntroPlaying);
        public int CurrentIndex => current;
        public int Hits => hits;
        public float Remaining => remaining;
        public float HoldProgress => Mathf.Clamp01(hold / 2);
        public bool Finished => finished;
        public bool Passed => passed;
        public string Status => status;
        public ParkingLevel CurrentLevel => Level;
        public void SetMenuOpen(bool open) { menuOpen = open; car.mobileControls?.ClearControls(); }
        void OnApplicationPause(bool paused) { suspended = paused; if (paused) PlayerPrefs.Save(); }
        void OnApplicationFocus(bool focus) { if (Application.isMobilePlatform) suspended = !focus; }
        ParkingLevel Level => catalog != null ? activeLevel : levels[current];
        void Start() { car.course = this; SelectLevel(PlayerPrefs.GetInt(LastPlayedKey, startingLevel)); }
        public void SelectLevel(int index)
        {
            int selected = Mathf.Clamp(index, 0, LevelCount - 1);
            if (catalog != null)
            {
                if (activeLevel == null || selected != current)
                {
                    if (activeLevel != null)
                    {
                        activeLevel.gameObject.SetActive(false);
                        Destroy(activeLevel.gameObject);
                    }
                    activeLevel = catalog.CreateLevel(selected, transform);
                }
            }
            else for (int i = 0; i < levels.Length; i++) levels[i].gameObject.SetActive(i == selected);
            current = selected;
            PlayerPrefs.SetInt(LastPlayedKey,current);
            car.ResetAt(Level.spawn);
            menuOpen = false;
            overview.GetComponent<ParkingCamera>()?.BeginLevel();
            remaining = Level.timeLimit;
            hits = 0; hold = 0; finished = passed = reversedIntoBay = false; status = "";
        }
        public void RegisterCollision()
        {
            if (finished || IsPaused) return;
            hits++;
            if (hits >= Level.collisionLimit) Finish(false, "Collision limit reached");
        }
        void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.rKey.wasPressedThisFrame) SelectLevel(current);
            if (finished || IsPaused) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) { Finish(false, "Time is up"); return; }
            Vector3 local = Level.bay.InverseTransformPoint(car.transform.position);
            float angle = Mathf.Abs(Mathf.DeltaAngle(car.transform.eulerAngles.y, Level.bay.eulerAngles.y));
            // Project the entire 1.8 x 3.8 metre car footprint into the bay, not just its centre.
            float radians = angle * Mathf.Deg2Rad;
            float halfX = Mathf.Abs(Mathf.Cos(radians)) * .9f + Mathf.Abs(Mathf.Sin(radians)) * 1.9f;
            float halfZ = Mathf.Abs(Mathf.Sin(radians)) * .9f + Mathf.Abs(Mathf.Cos(radians)) * 1.9f;
            bool inside = Mathf.Abs(local.x) + halfX <= Level.baySize.x / 2 && Mathf.Abs(local.z) + halfZ <= Level.baySize.y / 2;
            float signedSpeed = Vector3.Dot(car.GetComponent<Rigidbody>().linearVelocity, car.transform.forward);
            if (inside && signedSpeed < -.2f) reversedIntoBay = true;
            if (!inside) reversedIntoBay = false;
            bool aligned = angle <= Level.angleTolerance;
            bool valid = inside && aligned && car.Speed < .15f && (!Level.reverseEntry || reversedIntoBay);
            hold = valid ? hold + Time.deltaTime : 0;
            status = inside ? !aligned ? "Align with the bay arrow" : Level.reverseEntry && !reversedIntoBay ? "Reverse into this bay" : car.Speed >= .15f ? "Brake and stop completely" : "Hold still..." : "Follow the arrow inside the green bay";
            if (hold >= 2) Finish(true, "Perfectly parked!");
        }
        void Finish(bool success, string message) { finished = true; passed = success; status = message; car.driving = false; }
    }
}
