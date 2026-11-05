using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CityParking;

[InitializeOnLoad]
public static class ParkingValidation
{
    const string Key = "CityParking.Validation";
    static double start;
    static int index = -1;
    static readonly int[] Samples = { 0,1,2,3,4,5,6,7,8,9,10,99,249,498,499 };
    static ParkingCourse course;
    static bool portraitReady;
    static ParkingValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool(Key + ".HadProgress",PlayerPrefs.HasKey("CityParking.LastPlayedLevel"));
        SessionState.SetInt(Key + ".Progress",PlayerPrefs.GetInt("CityParking.LastPlayedLevel",0));
        SetPortraitGameView();
        EditorSceneManager.OpenScene("Assets/Scenes/SuburbanParking.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static void RestoreProgress()
    {
        if (SessionState.GetBool(Key + ".HadProgress",false)) PlayerPrefs.SetInt("CityParking.LastPlayedLevel",SessionState.GetInt(Key + ".Progress",0));
        else PlayerPrefs.DeleteKey("CityParking.LastPlayedLevel");
        PlayerPrefs.Save();
    }
    static void SetPortraitGameView()
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).GetValue(null);
        var getGroup = sizesType.GetMethod("GetGroup");
        var group = getGroup.Invoke(sizes,new[]{Enum.Parse(getGroup.GetParameters()[0].ParameterType,"Standalone")});
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.Parse(kind,"FixedResolution"),720,1280,"Portrait Validation"},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var gameViewType = assembly.GetType("UnityEditor.GameView");
        var window = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(window,total-1);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (!course)
            {
                course = UnityEngine.Object.FindFirstObjectByType<ParkingCourse>();
                if (!course || !course.car) return;
                if (course.LevelCount != 500) throw new Exception("Expected 500 levels");
                index = -1;
                start = EditorApplication.timeSinceStartup;
            }
            if (EditorApplication.timeSinceStartup - start < 5) return;
            if (index == -1)
            {
                if (!portraitReady)
                {
                var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                var gameView = EditorWindow.GetWindow(gameViewType);
                var render = gameViewType.GetMethod("RenderView",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Vector2),typeof(bool)},null);
                render?.Invoke(gameView,new object[]{Vector2.zero,false});
                typeof(ParkingMobileHUD).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(course.car.mobileControls,null);
                typeof(ParkingCamera).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(course.overview.GetComponent<ParkingCamera>(),null);
                Debug.Log($"PORTRAIT_TEST_SCREEN: {Screen.width} x {Screen.height}");
                portraitReady = true;
                start = EditorApplication.timeSinceStartup - 4;
                return; // Let CanvasScaler and safe-area layout settle after resizing the Game view.
                }
                ParkingMobileValidation.Verify(course);
                if (!SystemInfo.graphicsDeviceType.Equals(UnityEngine.Rendering.GraphicsDeviceType.Null))
                    ScreenCapture.CaptureScreenshot("/tmp/city-parking-mobile.png");
            }
            if (index >= 0 && course.car.driving) throw new Exception("Parking completion failed for level " + (Samples[index] + 1));
            index++;
            if (index == Samples.Length)
            {
                course.SelectLevel(9);
                var rig = course.overview.GetComponent<ParkingCamera>();
                typeof(ParkingCamera).GetField("introElapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rig,rig.introDuration);
                typeof(ParkingCamera).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rig,null);
                for (int hit = 0; hit < course.CurrentLevel.collisionLimit; hit++) course.RegisterCollision();
                if (course.car.driving) throw new Exception("Collision limit did not stop driving");
                course.SelectLevel(0);
                if (!course.car.driving) throw new Exception("Restart did not restore driving");
                File.WriteAllText("/tmp/city-parking-test-result.txt", "PASS: 15 sampled parking completions across 500 levels, reverse entry, collision failure, restart, mobile input, safe area, and isometric framing.\n");
                RestoreProgress(); SessionState.SetBool(Key,false); EditorApplication.Exit(0); return;
            }
            course.SelectLevel(Samples[index]);
            if (UnityEngine.Object.FindObjectsByType<ParkingLevel>(FindObjectsSortMode.None).Length != 1) throw new Exception("More than one live level");
            course.car.enabled = false;
            var level = course.CurrentLevel;
            var rb = course.car.GetComponent<Rigidbody>();
            course.car.transform.SetPositionAndRotation(level.bay.position + Vector3.up*.04f, level.bay.rotation);
            rb.position = course.car.transform.position; rb.rotation = level.bay.rotation;
            if (level.reverseEntry)
            {
                var rig = course.overview.GetComponent<ParkingCamera>();
                typeof(ParkingCamera).GetField("introElapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rig,rig.introDuration);
                typeof(ParkingCamera).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rig,null);
                rb.linearVelocity = -course.car.transform.forward * .5f;
                typeof(ParkingCourse).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(course,null);
            }
            rb.linearVelocity = Vector3.zero;
            start = EditorApplication.timeSinceStartup;
        }
        catch (Exception e)
        {
            File.WriteAllText("/tmp/city-parking-test-result.txt", "FAIL: " + e);
            Debug.LogException(e); RestoreProgress(); SessionState.SetBool(Key,false); EditorApplication.Exit(1);
        }
    }
}
