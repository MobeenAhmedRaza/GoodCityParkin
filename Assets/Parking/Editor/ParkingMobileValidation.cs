using System;
using System.Reflection;
using CityParking;
using UnityEngine;
using UnityEngine.EventSystems;

public static class ParkingMobileValidation
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Verify(ParkingCourse course)
    {
        Check(Screen.height > Screen.width, "Validation must run in portrait");
        var hud = course.car.mobileControls;
        Check(hud != null, "Mobile HUD missing");
        var buttons = hud.GetComponentsInChildren<ParkingTouchButton>();
        Check(buttons.Length == 5, "Expected five touch controls");
        ParkingTouchButton drive = Array.Find(buttons,b=>b.name=="DRIVE");
        ParkingTouchButton left = Array.Find(buttons,b=>b.name=="LEFT");
        var first = new PointerEventData(EventSystem.current) { pointerId=11 };
        var second = new PointerEventData(EventSystem.current) { pointerId=12 };
        drive.OnPointerDown(first); left.OnPointerDown(second);
        Check(hud.Throttle == 1 && hud.Steering == -1, "Simultaneous drive and steering failed");
        drive.OnPointerUp(first);
        Check(hud.Throttle == 0 && hud.Steering == -1, "Releasing one thumb released another control");
        left.OnPointerExit(second);
        Check(hud.Steering == 0, "Leaving the control did not release steering");
        drive.OnPointerDown(first); hud.ClearControls();
        Check(hud.Throttle == 0, "Touch reset failed");
        course.SetMenuOpen(true);
        float remaining = course.Remaining;
        typeof(ParkingCourse).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(course,null);
        Check(course.Remaining == remaining, "Level timer ran while menu was open");
        course.SetMenuOpen(false);
        Canvas.ForceUpdateCanvases();
        // Game view display pixels can differ from its render resolution in the editor.
        // Compare controls with their actual safe-area parent in the same coordinate space.
        var safeRoot = (RectTransform)hud.GetComponentInChildren<Canvas>().transform.Find("Safe Area");
        var safe = safeRoot.rect;
        foreach(var b in buttons)
        {
            var corners = new Vector3[4]; b.GetComponent<RectTransform>().GetWorldCorners(corners);
            foreach(var world in corners)
            {
                var c = safeRoot.InverseTransformPoint(world);
                Check(c.x >= safe.xMin-1 && c.x <= safe.xMax+1 && c.y >= safe.yMin-1 && c.y <= safe.yMax+1, "Touch control outside safe area: " + b.name);
            }
        }
        var rig = course.overview.GetComponent<ParkingCamera>();
        Check(rig != null && course.overview.orthographic,"Isometric camera missing");
        Check(Mathf.Abs(Mathf.DeltaAngle(course.overview.transform.eulerAngles.x,ParkingCamera.IsometricPitch))<.01f,"Incorrect isometric pitch");
        foreach(float aspect in new[]{3f/4,9f/16,9f/19.5f,9f/20})
            for(int q=0;q<4;q++)
            {
                var rotation=Quaternion.Euler(ParkingCamera.IsometricPitch,45+q*90,0);
                float size=ParkingCamera.CourseSize(rotation,aspect);
                for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
                {
                    var p=Quaternion.Inverse(rotation)*new Vector3(x*18,0,z*23);
                    Check(Mathf.Abs(p.x)<size*aspect*.9f && Mathf.Abs(p.y)<size*.6f,"Course is clipped at aspect " + aspect);
                }
            }
        var slider = hud.GetComponentInChildren<UnityEngine.UI.Slider>(true);
        Check(slider != null, "Zoom slider missing");
        slider.value = 0;
        Check(Mathf.Abs(rig.drivingSize - ParkingCamera.MaxZoomSize) < .01f, "Zoom out slider endpoint failed");
        slider.value = 1;
        Check(Mathf.Abs(rig.drivingSize - ParkingCamera.MinZoomSize) < .01f, "Zoom in slider endpoint failed");
        rig.SetZoom(.5f);
        course.SelectLevel(0);
        Check(rig.IntroPlaying && course.IsPaused, "Level start did not start the intro and pause gameplay");
        Check(Mathf.Abs(rig.Zoom01-.5f)<.01f,"Restart discarded selected zoom");
        var late = typeof(ParkingCamera).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        var elapsed = typeof(ParkingCamera).GetField("introElapsed",BindingFlags.Instance|BindingFlags.NonPublic);
        late.Invoke(rig,null);
        float wide = course.overview.orthographicSize;
        Check(Mathf.Abs(rig.introDuration - 1.5f) < .001f, "Intro duration must be 1.5 seconds");
        Check(Quaternion.Angle(rig.transform.rotation, rig.IntroRotation) < .01f, "Intro did not start overhead");
        foreach (float aspect in new[] { 3f/4, 9f/16, 9f/19.5f, 9f/20 })
        {
            float size = ParkingCamera.CourseSize(rig.IntroRotation, aspect);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                var point = Quaternion.Inverse(rig.IntroRotation) * new Vector3(x * 18, 0, z * 23);
                Check(Mathf.Abs(point.x) < size * aspect * .9f && Mathf.Abs(point.y) < size * .6f,
                    "Overhead course view clipped at aspect " + aspect);
            }
        }
        elapsed.SetValue(rig,rig.introDuration*.5f); late.Invoke(rig,null);
        float middle = course.overview.orthographicSize;
        Check(Quaternion.Angle(rig.transform.rotation, Quaternion.Slerp(rig.IntroRotation, rig.ViewRotation, .5f)) < .01f,
            "Intro did not interpolate camera tilt");
        elapsed.SetValue(rig,rig.introDuration); late.Invoke(rig,null);
        Check(wide > middle && middle > course.overview.orthographicSize, "Intro did not zoom from wide to close");
        Check(!rig.IntroPlaying && !course.IsPaused, "Intro did not release gameplay");
        Check(Quaternion.Angle(rig.transform.rotation, rig.ViewRotation) < .01f, "Intro did not finish at driving angle");
        var hudType = typeof(ParkingMobileHUD);
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        hudType.GetMethod("ShowLevels",privateInstance).Invoke(hud,new object[]{true});
        var nextPage = (UnityEngine.UI.Button)hudType.GetField("nextPage",privateInstance).GetValue(hud);
        for (int page=1;page<50;page++) nextPage.onClick.Invoke();
        Check(!nextPage.interactable,"Level menu did not stop at the final page");
        var choices = (UnityEngine.UI.Button[])hudType.GetField("levelButtons",privateInstance).GetValue(hud);
        choices[9].onClick.Invoke();
        Check(course.CurrentIndex==499,"Last page did not select level 500");
        hudType.GetMethod("ShowLevels",privateInstance).Invoke(hud,new object[]{true});
        var jump = (UnityEngine.UI.InputField)hudType.GetField("jumpInput",privateInstance).GetValue(hud);
        jump.text="250";
        hudType.GetMethod("JumpToLevel",privateInstance).Invoke(hud,null);
        Check(course.CurrentIndex==249,"Jump-to-level selected the wrong level");
        var zoomPanel = (GameObject)hudType.GetField("zoomPanel",privateInstance).GetValue(hud);
        var zoomButton = (UnityEngine.UI.Button)hudType.GetField("zoomButton",privateInstance).GetValue(hud);
        var updateHud = hudType.GetMethod("Update",privateInstance);
        foreach (int index in new[] { 0, 4 })
        {
            course.SelectLevel(index);
            Check(zoomPanel.activeSelf && !zoomButton.gameObject.activeSelf, "First five levels must show the zoom bar");
            drive.OnPointerDown(first); updateHud.Invoke(hud,null); drive.OnPointerUp(first);
            Check(zoomPanel.activeSelf, "Acceleration hid the tutorial zoom bar");
        }
        course.SelectLevel(5);
        Check(!zoomPanel.activeSelf && zoomButton.gameObject.activeSelf, "Level 6 must start with the Zoom button only");
        zoomButton.onClick.Invoke();
        Check(zoomPanel.activeSelf, "Zoom button did not open slider");
        zoomButton.onClick.Invoke();
        Check(!zoomPanel.activeSelf, "Second Zoom tap did not close slider");
        zoomButton.onClick.Invoke();
        drive.OnPointerDown(first); updateHud.Invoke(hud,null); drive.OnPointerUp(first);
        Check(!zoomPanel.activeSelf, "Acceleration did not close zoom slider");
        zoomButton.onClick.Invoke();
        course.SelectLevel(5);
        Check(!zoomPanel.activeSelf, "Restart left zoom slider open");
        Check(Mathf.Abs(rig.Zoom01-.5f)<.01f, "Toggling zoom discarded selected zoom");
        Canvas.ForceUpdateCanvases();
        var row = zoomButton.transform.parent;
        var levelsRect = (RectTransform)row.Find("Levels");
        var restartRect = (RectTransform)row.Find("Restart");
        var zoomRect = (RectTransform)zoomButton.transform;
        Check(Mathf.Abs(levelsRect.rect.width - zoomRect.rect.width) < 1 &&
            Mathf.Abs(restartRect.rect.width - zoomRect.rect.width) < 1 &&
            levelsRect.localPosition.x < restartRect.localPosition.x && restartRect.localPosition.x < zoomRect.localPosition.x &&
            Mathf.Abs(levelsRect.localPosition.y - zoomRect.localPosition.y) < 1,
            "Levels, Restart and Zoom must share an evenly sized row");
        course.SelectLevel(0);
        Check(zoomPanel.activeSelf && !zoomButton.gameObject.activeSelf, "Returning to early levels did not restore slider");
        Debug.Log("CITY_PARKING_ZOOM_TESTS_PASS: level 5/6 boundary, toggle, acceleration dismissal, restart, zoom persistence and action row.");
        Debug.Log("CITY_PARKING_MOBILE_TESTS_PASS: simultaneous touch, independent release, reset, pause, safe area, portrait framing, zoom slider endpoints, intro animation, restart zoom persistence, 50 menu pages, and jump-to-level.");
    }
}
