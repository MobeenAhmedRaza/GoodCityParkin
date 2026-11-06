using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CityParking;

[InitializeOnLoad]
public static class ParkingControlsValidation
{
    const string Key = "CityParking.ControlsValidation";
    static readonly string[] Prefs = { ParkingMobileHUD.TutorialKey, ParkingMobileHUD.ModeKey, "CityParking.LastPlayedLevel" };
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool running;
    static ParkingControlsValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        foreach (var key in Prefs)
        {
            SessionState.SetBool(Key + key + "exists", PlayerPrefs.HasKey(key));
            SessionState.SetInt(Key + key, PlayerPrefs.GetInt(key));
            PlayerPrefs.DeleteKey(key);
        }
        typeof(ParkingValidation).GetMethod("SetPortraitGameView",BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
        EditorSceneManager.OpenScene("Assets/Scenes/SuburbanParking.unity");
        SessionState.SetBool(Key,true);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || running || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var course = UnityEngine.Object.FindFirstObjectByType<ParkingCourse>();
        if (course == null || course.CurrentLevel == null || course.car.mobileControls == null) return;
        running = true;
        course.car.mobileControls.StartCoroutine(RunChecks(course));
    }
    static IEnumerator RunChecks(ParkingCourse course)
    {
        var checks = Verify(course);
        while (true)
        {
            object next;
            try
            {
                if (!checks.MoveNext()) { Finish(null); yield break; }
                next = checks.Current;
            }
            catch (Exception e) { Finish(e); yield break; }
            yield return next;
        }
    }
    static void Finish(Exception error)
    {
        foreach (var key in Prefs)
        {
            if (SessionState.GetBool(Key + key + "exists",false)) PlayerPrefs.SetInt(key,SessionState.GetInt(Key+key,0));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save(); SessionState.SetBool(Key,false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("CITY_PARKING_CONTROLS_PASS: tutorial ordering, failure/retry, automatic wheel trial, choice, persistence, touch steering, pointer ownership, forward/reverse, release stop, pause clearing and switching.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    static object Call(object target,string name,params object[] args) => target.GetType().GetMethod(name,Private).Invoke(target,args);
    static void EndIntro(ParkingCourse course)
    {
        var rig = course.overview.GetComponent<ParkingCamera>();
        typeof(ParkingCamera).GetField("introElapsed",Private).SetValue(rig,rig.introDuration);
        Call(rig,"LateUpdate");
    }
    static IEnumerator Verify(ParkingCourse course)
    {
        yield return null;
        var hud = course.car.mobileControls;
        var body = course.car.GetComponent<Rigidbody>();
        Check(course.CurrentIndex == 0 && !hud.WheelMode && hud.NeedsControlTutorial,"Introduction must start at level 1 with buttons");
        Call(course,"Finish",false,"Test failure"); course.SelectLevel(0);
        Check(!hud.WheelMode && PlayerPrefs.GetInt(ParkingMobileHUD.TutorialKey,0)==0,"Failure advanced tutorial");
        course.SelectLevel(5); Check(course.CurrentIndex==0,"Tutorial allowed skipping trials");
        Call(course,"Finish",true,"Test buttons pass");
        Check(PlayerPrefs.GetInt(ParkingMobileHUD.TutorialKey)==1,"Buttons pass not saved");
        yield return new WaitForSecondsRealtime(1.7f);
        Check(course.CurrentIndex==0 && !course.Finished && hud.WheelMode,"Level 1 did not restart with wheel");
        EndIntro(course);
        ScreenCapture.CaptureScreenshot("/tmp/city-parking-wheel.png");
        yield return null;
        Canvas.ForceUpdateCanvases();
        var wheel = hud.GetComponentInChildren<ParkingSteeringWheel>();
        Check(wheel != null && wheel.visual.GetComponent<Image>().sprite != null,"Wheel sprite missing");
        var rect = (RectTransform)wheel.transform;
        var finger = new PointerEventData(EventSystem.current) { pointerId=101,
            position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(0,80,0))) };
        wheel.OnPointerDown(finger);
        Check(hud.Throttle==1 && !hud.Braking,"Holding wheel did not accelerate forward");
        finger.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(80,0,0)));
        wheel.OnDrag(finger); Check(hud.Steering > .5f,"Clockwise wheel drag did not steer right");
        wheel.OnPointerUp(new PointerEventData(EventSystem.current) { pointerId=202 });
        Check(wheel.Held,"Another finger released the wheel");
        Call(course.car,"Update"); Call(course.car,"FixedUpdate");
        Check(Vector3.Dot(body.linearVelocity,course.car.transform.forward)>0,"Wheel did not move car forward");
        wheel.OnPointerUp(finger);
        Check(hud.Throttle==0 && hud.Braking && body.linearVelocity==Vector3.zero,"Releasing wheel did not stop immediately");
        Call(hud,"ToggleGear"); wheel.OnPointerDown(finger);
        Check(hud.Throttle==-1,"Reverse gear did not reverse throttle");
        Call(course.car,"Update"); Call(course.car,"FixedUpdate");
        Check(Vector3.Dot(body.linearVelocity,course.car.transform.forward)<0,"Reverse gear did not move car backward");
        course.SetMenuOpen(true);
        Check(!wheel.Held && body.linearVelocity==Vector3.zero,"Pause retained wheel input or motion");
        wheel.OnPointerDown(finger); Check(!wheel.Held,"Wheel accepted paused input");
        course.SetMenuOpen(false);
        Call(course,"Finish",false,"Test wheel failure"); course.SelectLevel(0);
        Check(hud.WheelMode,"Retry reverted wheel trial to buttons");
        EndIntro(course);
        body.position = course.CurrentLevel.bay.position + Vector3.up * .04f;
        body.rotation = course.CurrentLevel.bay.rotation;
        course.car.transform.SetPositionAndRotation(body.position,body.rotation);
        course.car.StopMotion();
        yield return new WaitForSeconds(2.5f);
        Check(course.Finished && course.Passed,"Releasing the wheel inside the bay did not complete parking");
        ScreenCapture.CaptureScreenshot("/tmp/city-parking-control-choice.png");
        yield return null;
        Check(PlayerPrefs.GetInt(ParkingMobileHUD.TutorialKey)==2 && course.IsPaused,"Second trial did not pause for choice");
        Check(hud.transform.Find("Mobile Parking UI/Safe Area/Choose controls").gameObject.activeSelf,"Control chooser missing");
        // A reload while choosing must preserve both completed trials.
        Call(hud,"LoadControlPreference"); course.SelectLevel(0);
        Check(course.IsPaused && hud.WheelMode,"Pending choice was lost on reload");
        Call(hud,"ChooseControls",true);
        Check(!hud.NeedsControlTutorial && course.CurrentIndex==1 && hud.WheelMode,"Choice did not advance to level 2");
        Check(PlayerPrefs.GetInt(ParkingMobileHUD.ModeKey)==1,"Wheel choice not saved");
        EndIntro(course); Call(hud,"ShowControlChoice"); Call(hud,"ChooseControls",false);
        Check(!hud.WheelMode && !course.IsPaused && course.CurrentIndex==1,"Switching to buttons changed level or left game paused");
        Call(hud,"LoadControlPreference"); Check(!hud.WheelMode,"Saved button preference not loaded");
        Call(hud,"ShowControlChoice"); Call(hud,"ChooseControls",true);
        Check(hud.WheelMode && !hud.WheelHeld,"Switching to wheel retained throttle");
        course.SelectLevel(0); Check(hud.WheelMode && !hud.NeedsControlTutorial,"Completed introduction replayed on level 1");
        // Existing mobile/zoom checks run with the tutorial completed and buttons selected.
        Call(hud,"ShowControlChoice"); Call(hud,"ChooseControls",false);
        EndIntro(course);
        yield return new WaitForSecondsRealtime(1);
        ParkingMobileValidation.Verify(course);
    }
}
