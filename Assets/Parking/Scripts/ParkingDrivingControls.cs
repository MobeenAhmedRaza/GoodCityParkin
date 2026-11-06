using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CityParking
{
    public partial class ParkingMobileHUD
    {
        public const string TutorialKey = "CityParking.ControlsTutorial";
        public const string ModeKey = "CityParking.ControlMode";
        // 0: buttons trial, 1: wheel trial, 2: choose, 3: introduction complete.
        int tutorialStage;
        bool wheelMode, reverseGear, switchingTrial;
        ParkingSteeringWheel wheel;
        GameObject buttonControls, wheelControls, controlChoice;
        RectTransform feedback;
        Button modeButton, levelsButton;
        Image gearImage;
        Text gearLabel, choiceTitle;
        Sprite forwardGearSprite, reverseGearSprite;
        Coroutine trialTransition;
        public bool NeedsControlTutorial => tutorialStage < 3;
        public bool WheelMode => wheelMode;
        public bool WheelHeld => wheel != null && wheel.Held;

        void LoadControlPreference()
        {
            tutorialStage = Mathf.Clamp(PlayerPrefs.GetInt(TutorialKey, 0), 0, 3);
            wheelMode = tutorialStage == 0 ? false : tutorialStage < 3 || PlayerPrefs.GetInt(ModeKey, 0) == 1;
        }
        void SaveTutorialStage(int stage)
        {
            tutorialStage = stage;
            PlayerPrefs.SetInt(TutorialKey, stage);
            PlayerPrefs.Save();
        }
        void BuildWheelControls()
        {
            var root = Rect("Wheel controls",safeRoot,Vector2.zero,new Vector2(1,0),new Vector2(18,22),new Vector2(-18,260));
            wheelControls = root.gameObject;
            var touch = Panel("Steering wheel",root,new Vector2(.3f,.5f),new Vector2(.3f,.5f),new Vector2(-110,-110),new Vector2(110,110),Color.clear);
            var icon = Panel("Wheel visual",touch,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.white).GetComponent<Image>();
            icon.sprite = Resources.Load<Sprite>("steering-wheel"); icon.preserveAspect = true; icon.raycastTarget = false;
            wheel = touch.gameObject.AddComponent<ParkingSteeringWheel>();
            wheel.visual = icon.rectTransform;
            wheel.canDrive = () => wheelMode && !course.IsPaused && !course.Finished;
            wheel.released = () => course.car.StopMotion();
            var gear = Action("",root,new Vector2(.77f,.5f),new Vector2(.77f,.5f),new Vector2(-76,-112),new Vector2(76,112),ToggleGear);
            gear.name = "Gearshift";
            gearLabel = gear.GetComponentInChildren<Text>();
            gearLabel.rectTransform.anchorMax = new Vector2(1,0);
            gearLabel.rectTransform.offsetMin = new Vector2(4,4); gearLabel.rectTransform.offsetMax = new Vector2(-4,48);
            gearLabel.fontSize = 19;
            gearImage = Panel("Gear icon",gear.transform,Vector2.zero,Vector2.one,new Vector2(34,52),new Vector2(-34,-12),Color.white).GetComponent<Image>();
            gearImage.preserveAspect = true; gearImage.raycastTarget = false;
            foreach (var sprite in Resources.LoadAll<Sprite>("GearShift"))
            {
                if (sprite.name == "GearShift_0") forwardGearSprite = sprite;
                if (sprite.name == "GearShift_1") reverseGearSprite = sprite;
            }
            RefreshGear();
        }
        void ToggleGear()
        {
            if (course.IsPaused || course.Finished) return;
            ClearControls(); course.car.StopMotion();
            reverseGear = !reverseGear;
            RefreshGear();
        }
        void RefreshGear()
        {
            gearImage.sprite = reverseGear ? reverseGearSprite : forwardGearSprite;
            gearLabel.text = reverseGear ? "REVERSE\nTap to shift" : "FORWARD\nTap to shift";
        }
        void ApplyControlMode()
        {
            ClearControls();
            buttonControls.SetActive(!wheelMode);
            wheelControls.SetActive(wheelMode);
            feedback.offsetMin = new Vector2(-324,wheelMode ? 276 : 158);
            feedback.offsetMax = new Vector2(324,wheelMode ? 346 : 228);
            modeButton.GetComponentInChildren<Text>().text = NeedsControlTutorial ? (wheelMode ? "Wheel 2/2" : "Buttons 1/2") : "Controls";
            modeButton.interactable = !NeedsControlTutorial;
            levelsButton.interactable = !NeedsControlTutorial;
        }
        void BuildControlChoice()
        {
            controlChoice = Panel("Choose controls",safeRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.035f,.07f,.10f,.98f)).gameObject;
            choiceTitle = Label("Title",controlChoice.transform,new Vector2(0,.55f),new Vector2(1,.75f),new Vector2(24,0),new Vector2(-24,0),28,TextAnchor.MiddleCenter);
            var buttons = Rect("Choices",controlChoice.transform,new Vector2(0,.4f),new Vector2(1,.5f),new Vector2(24,0),new Vector2(-24,0));
            HorizontalLayout(buttons);
            foreach (bool useWheel in new[] { false, true })
            {
                bool selection = useWheel;
                var button = Action(useWheel ? "Steering wheel" : "Buttons",buttons,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,()=>ChooseControls(selection));
                button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                button.GetComponentInChildren<Text>().fontSize = 26;
            }
            Label("Help",controlChoice.transform,new Vector2(0,.2f),new Vector2(1,.37f),new Vector2(24,0),new Vector2(-24,0),22,TextAnchor.MiddleCenter).text =
                "Wheel: hold and turn to drive; release to stop.\nTap the gearshift for forward or reverse.\nYou can change controls later with Controls.";
            controlChoice.SetActive(false);
        }
        void ShowControlChoice()
        {
            if (switchingTrial || (NeedsControlTutorial && tutorialStage != 2)) return;
            choiceTitle.text = tutorialStage == 2 ? "BOTH TRIALS COMPLETE!\nChoose your driving controls" : "CHOOSE DRIVING CONTROLS";
            course.SetMenuOpen(true); course.car.StopMotion();
            controlChoice.SetActive(true);
        }
        void ChooseControls(bool useWheel)
        {
            bool completedTrials = tutorialStage == 2;
            wheelMode = useWheel;
            PlayerPrefs.SetInt(ModeKey, useWheel ? 1 : 0);
            SaveTutorialStage(3);
            controlChoice.SetActive(false);
            course.SetMenuOpen(false); course.car.StopMotion();
            ApplyControlMode();
            if (completedTrials) course.SelectLevel(Mathf.Min(1,course.LevelCount-1));
        }
        public void OnControlLevelStarted()
        {
            if (trialTransition != null) { StopCoroutine(trialTransition); trialTransition = null; }
            switchingTrial = false;
            if (NeedsControlTutorial) wheelMode = tutorialStage != 0;
            reverseGear = false; RefreshGear();
            controlChoice.SetActive(false);
            ApplyControlMode();
            if (tutorialStage == 2) ShowControlChoice();
        }
        public void OnControlLevelFinished(bool success)
        {
            ClearControls();
            if (!success || course.CurrentIndex != 0 || !NeedsControlTutorial) return;
            if (tutorialStage == 0)
            {
                SaveTutorialStage(1);
                switchingTrial = true;
                trialTransition = StartCoroutine(StartWheelTrial());
            }
            else if (tutorialStage == 1)
            {
                SaveTutorialStage(2);
                ShowControlChoice();
            }
        }
        IEnumerator StartWheelTrial()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            trialTransition = null;
            course.SelectLevel(0);
        }
    }
}
