using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityParking
{
    public class ParkingMobileHUD : MonoBehaviour
    {
        public ParkingCourse course;
        public ParkingCamera cameraRig;
        ParkingTouchButton left, right, forward, reverse, brake;
        RectTransform safeRoot;
        Text heading, objective, stats, status, resultText;
        Image progress;
        Slider zoomSlider;
        GameObject zoomPanel;
        Button zoomButton;
        bool zoomExpanded;
        GameObject levelMenu, resultPanel, nextButton;
        Font font;
        const int PageSize = 10;
        int levelPage;
        Button[] levelButtons;
        Text pageLabel;
        InputField jumpInput;
        Button previousPage, nextPage;
        Rect lastSafe;
        Vector2Int lastScreen;
        public float Throttle => (forward && forward.Held ? 1 : 0) - (reverse && reverse.Held ? 1 : 0);
        public float Steering => (right && right.Held ? 1 : 0) - (left && left.Held ? 1 : 0);
        public bool Braking => brake && brake.Held;
        readonly Color ink = new Color(.07f,.12f,.16f,.94f);
        readonly Color mint = new Color(.36f,.94f,.72f);
        void Awake()
        {
            if (Application.isMobilePlatform)
            {
                Screen.autorotateToPortrait = true; Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = false;
                Screen.orientation = ScreenOrientation.Portrait;
                Application.targetFrameRate = 60;
            }
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null)
            {
                var events = new GameObject("Touch Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var canvas = new GameObject("Mobile Parking UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720,1280); scaler.matchWidthOrHeight = 1;
            safeRoot = Rect("Safe Area",canvas.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var top = Panel("Header",safeRoot,new Vector2(0,1),Vector2.one,new Vector2(18,-178),new Vector2(-18,-18),ink);
            heading = Label("Level",top,new Vector2(0,1),Vector2.one,new Vector2(18,-48),new Vector2(-18,-8),28,TextAnchor.MiddleLeft);
            objective = Label("Objective",top,new Vector2(0,1),Vector2.one,new Vector2(18,-109),new Vector2(-18,-53),23,TextAnchor.MiddleLeft);
            stats = Label("Stats",top,Vector2.zero,new Vector2(1,0),new Vector2(18,8),new Vector2(-18,43),22,TextAnchor.MiddleLeft);
            var actions = Rect("Menu controls",safeRoot,new Vector2(0,1),Vector2.one,new Vector2(18,-244),new Vector2(-18,-190));
            HorizontalLayout(actions);
            var levelsAction = Action("Levels",actions,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,()=>ShowLevels(true));
            var restartAction = Action("Restart",actions,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,()=>{ course.SelectLevel(course.CurrentIndex); ClearControls(); });
            zoomButton = Action("Zoom",actions,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,()=>
            {
                zoomExpanded = !zoomExpanded;
                RefreshZoomVisibility();
            });
            foreach (var button in new[] { levelsAction, restartAction, zoomButton })
            {
                var sizing = button.gameObject.AddComponent<LayoutElement>();
                sizing.minWidth = sizing.preferredWidth = 0;
                sizing.flexibleWidth = 1;
            }
            BuildZoomSlider();
            var controls = Rect("Driving Controls",safeRoot,Vector2.zero,new Vector2(1,0),new Vector2(18,22),new Vector2(-18,142));
            HorizontalLayout(controls);
            var steering = ControlGroup("Steering",controls,2);
            left = Control("LEFT",steering,Vector2.zero,Vector2.one,"left");
            right = Control("RIGHT",steering,Vector2.zero,Vector2.one,"right");
            var pedals = ControlGroup("Pedals",controls,3);
            brake = Control("BRAKE",pedals,Vector2.zero,Vector2.one,"brake");
            reverse = Control("REVERSE",pedals,Vector2.zero,Vector2.one,"reverse");
            forward = Control("DRIVE",pedals,Vector2.zero,Vector2.one,"accelerate");
            var feedback = Panel("Parking feedback",safeRoot,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-324,158),new Vector2(324,228),ink);
            status = Label("Status",feedback,Vector2.zero,Vector2.one,new Vector2(12,10),new Vector2(-12,-5),22,TextAnchor.MiddleCenter);
            progress = Panel("Hold progress",feedback,Vector2.zero,Vector2.zero,new Vector2(0,0),new Vector2(0,5),mint).GetComponent<Image>();
            BuildLevelMenu(); BuildResult();
            course.car.mobileControls = this;
            ApplySafeArea();
        }
        void Update()
        {
            ApplySafeArea();
            var level = course.CurrentLevel;
            heading.text = $"{course.CurrentIndex+1:000} / {course.LevelCount}   {level.title}";
            objective.text = level.instruction;
            stats.text = $"{Mathf.CeilToInt(Mathf.Max(0,course.Remaining))}s remaining     Contacts  {course.Hits} / {level.collisionLimit}";
            status.text = string.IsNullOrEmpty(course.Status) ? "Park facing the arrow. Hold still for 2 seconds." : course.Status;
            progress.rectTransform.anchorMax = new Vector2(course.HoldProgress,0);
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (forward.Held || (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)))
                zoomExpanded = false;
            RefreshZoomVisibility();
            zoomSlider.interactable = !cameraRig.IntroPlaying;
            zoomSlider.SetValueWithoutNotify(cameraRig.Zoom01);
            if (cameraRig.IntroPlaying) status.text = "Get ready...";
            resultPanel.SetActive(course.Finished && !levelMenu.activeSelf);
            if (course.Finished)
            {
                resultText.text = course.Passed ? course.CurrentIndex == course.LevelCount - 1 ? $"ALL {course.LevelCount} LEVELS COMPLETE" : "PARKED!" : course.Status;
                nextButton.SetActive(course.Passed);
            }
        }
        const float ControlSpacing = 12;
        static void HorizontalLayout(RectTransform root)
        {
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ControlSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        }
        RectTransform ControlGroup(string name, RectTransform parent, int buttonCount)
        {
            var group = Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            HorizontalLayout(group);
            var sizing = group.gameObject.AddComponent<LayoutElement>();
            // Reserve each group's internal gaps, then share remaining space per button.
            sizing.minWidth = sizing.preferredWidth = ControlSpacing * (buttonCount - 1);
            sizing.flexibleWidth = buttonCount;
            return group;
        }
        public void ResetZoomVisibility()
        {
            zoomExpanded = false;
            if (zoomPanel != null) RefreshZoomVisibility();
        }
        void RefreshZoomVisibility()
        {
            bool alwaysVisible = course.CurrentIndex < 5;
            zoomButton.gameObject.SetActive(!alwaysVisible);
            zoomPanel.SetActive(alwaysVisible || zoomExpanded);
        }
        void BuildZoomSlider()
        {
            var panel = Panel("Camera zoom",safeRoot,new Vector2(0,1),Vector2.one,new Vector2(18,-324),new Vector2(-18,-256),ink);
            zoomPanel = panel.gameObject;
            ResetZoomVisibility();
            Label("Zoom label",panel,Vector2.zero,new Vector2(0,1),new Vector2(14,0),new Vector2(110,0),22,TextAnchor.MiddleLeft).text = "ZOOM";
            var root = Rect("Zoom slider",panel,Vector2.zero,Vector2.one,new Vector2(128,4),new Vector2(-24,-4));
            // A transparent raycast surface makes the whole 60-point row draggable.
            root.gameObject.AddComponent<Image>().color = Color.clear;
            Panel("Track",root,new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(0,-4),new Vector2(0,4),new Color(.32f,.40f,.44f));
            var fillArea = Rect("Fill area",root,new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(20,-4),new Vector2(-20,4));
            var fill = Panel("Fill",fillArea,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,mint);
            var handleArea = Rect("Handle area",root,new Vector2(0,.5f),new Vector2(1,.5f),new Vector2(20,0),new Vector2(-20,0));
            var handle = Panel("Handle",handleArea,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(-20,-25),new Vector2(20,25),mint);
            zoomSlider = root.gameObject.AddComponent<Slider>();
            zoomSlider.fillRect = fill; zoomSlider.handleRect = handle; zoomSlider.targetGraphic = handle.GetComponent<Image>();
            zoomSlider.minValue = 0; zoomSlider.maxValue = 1; zoomSlider.direction = Slider.Direction.LeftToRight;
            zoomSlider.SetValueWithoutNotify(cameraRig.Zoom01);
            zoomSlider.onValueChanged.AddListener(cameraRig.SetZoom);
        }
        void ApplySafeArea()
        {
            var safe = Screen.safeArea; var screen = new Vector2Int(Screen.width,Screen.height);
            if (safe == lastSafe && screen == lastScreen) return;
            if (screen.x <= 0 || screen.y <= 0) return;
            safeRoot.anchorMin = new Vector2(safe.xMin/screen.x,safe.yMin/screen.y);
            safeRoot.anchorMax = new Vector2(safe.xMax/screen.x,safe.yMax/screen.y);
            if (levelMenu != null)
            {
                Canvas.ForceUpdateCanvases();
                var card = (RectTransform)levelMenu.transform.Find("Menu");
                float fit = Mathf.Min(1, (safeRoot.rect.height - 32) / 920f, (safeRoot.rect.width - 32) / 648f);
                card.localScale = Vector3.one * Mathf.Max(.1f,fit);
            }
            lastSafe = safe; lastScreen = screen;
        }
        public void ClearControls()
        {
            left?.Clear(); right?.Clear(); forward?.Clear(); reverse?.Clear(); brake?.Clear();
        }
        void ShowLevels(bool open)
        {
            levelMenu.SetActive(open); course.SetMenuOpen(open); ClearControls();
            if (open) { levelPage = course.CurrentIndex / PageSize; RefreshLevelPage(); }
        }
        void RefreshLevelPage()
        {
            int lastPage = (course.LevelCount - 1) / PageSize;
            levelPage = Mathf.Clamp(levelPage,0,lastPage);
            pageLabel.text = $"{levelPage + 1} / {lastPage + 1}";
            previousPage.interactable = levelPage > 0; nextPage.interactable = levelPage < lastPage;
            for (int i=0;i<PageSize;i++)
            {
                int index = levelPage * PageSize + i;
                var button = levelButtons[i];
                button.gameObject.SetActive(index < course.LevelCount);
                if (index >= course.LevelCount) continue;
                button.GetComponentInChildren<Text>().text = $"{index+1:000}   {course.LevelTitle(index)}";
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(()=>{ course.SelectLevel(index); ShowLevels(false); });
            }
        }
        void JumpToLevel()
        {
            if (!int.TryParse(jumpInput.text,out int number)) return;
            number = Mathf.Clamp(number,1,course.LevelCount);
            course.SelectLevel(number-1); ShowLevels(false);
        }
        void BuildLevelMenu()
        {
            levelMenu = Panel("Level selection",safeRoot,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.035f,.07f,.10f,.97f)).gameObject;
            var card = Rect("Menu",levelMenu.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-324,-460),new Vector2(324,460));
            Label("Choose a level",card,new Vector2(0,1),Vector2.one,new Vector2(0,-50),Vector2.zero,28,TextAnchor.MiddleCenter).text = "CHOOSE A LEVEL";
            levelButtons = new Button[PageSize];
            for(int i=0;i<PageSize;i++)
                levelButtons[i] = Action("Level",card,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-114-i*61),new Vector2(648,-60-i*61),()=>{});
            previousPage = Action("Previous",card,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-732),new Vector2(210,-678),()=>{ levelPage--; RefreshLevelPage(); });
            pageLabel = Label("Page",card,new Vector2(0,1),new Vector2(0,1),new Vector2(218,-732),new Vector2(430,-678),22,TextAnchor.MiddleCenter);
            nextPage = Action("Next",card,new Vector2(0,1),new Vector2(0,1),new Vector2(438,-732),new Vector2(648,-678),()=>{ levelPage++; RefreshLevelPage(); });
            var input = Panel("Level number",card,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-800),new Vector2(432,-746),new Color(.15f,.23f,.28f));
            jumpInput = input.gameObject.AddComponent<InputField>();
            jumpInput.textComponent = Label("Input",input,Vector2.zero,Vector2.one,new Vector2(14,0),new Vector2(-14,0),22,TextAnchor.MiddleLeft);
            var placeholder = Label("Placeholder",input,Vector2.zero,Vector2.one,new Vector2(14,0),new Vector2(-14,0),22,TextAnchor.MiddleLeft);
            placeholder.text = $"Level number (1-{course.LevelCount})"; placeholder.color = new Color(.7f,.75f,.8f);
            jumpInput.placeholder = placeholder; jumpInput.contentType = InputField.ContentType.IntegerNumber; jumpInput.characterLimit = 6;
            Action("Go",card,new Vector2(0,1),new Vector2(0,1),new Vector2(446,-800),new Vector2(648,-746),JumpToLevel);
            Action("Back to game",card,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-180,0),new Vector2(180,62),()=>ShowLevels(false));
            RefreshLevelPage();
            levelMenu.SetActive(false);
        }
        void BuildResult()
        {
            resultPanel = Panel("Result",safeRoot,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-310,-95),new Vector2(310,95),ink).gameObject;
            resultText = Label("Result title",resultPanel.transform,new Vector2(0,.5f),Vector2.one,new Vector2(12,0),new Vector2(-12,-10),26,TextAnchor.MiddleCenter);
            var retryButton = Action("Try again",resultPanel.transform,Vector2.zero,new Vector2(.49f,.5f),new Vector2(14,14),new Vector2(-4,-4),()=>course.SelectLevel(course.CurrentIndex));
            nextButton = Action("Continue",resultPanel.transform,new Vector2(.51f,0),new Vector2(1,.5f),new Vector2(4,14),new Vector2(-14,-4),()=>course.SelectLevel((course.CurrentIndex+1)%course.LevelCount)).gameObject;
            retryButton.GetComponent<Image>().color = new Color32(0xCF, 0x61, 0x5B, 0xFF);
            retryButton.GetComponentInChildren<Text>().fontSize = 30;
            nextButton.GetComponent<Image>().color = new Color32(0x55, 0xB7, 0x46, 0xFF);
            nextButton.GetComponentInChildren<Text>().fontSize = 30;
            resultPanel.SetActive(false);
        }
        RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper)
        {
            var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin=min; r.anchorMax=max; r.offsetMin=lower; r.offsetMax=upper; return r;
        }
        RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper,Color color)
        {
            var r=Rect(name,parent,min,max,lower,upper); r.gameObject.AddComponent<Image>().color=color; return r;
        }
        Text Label(string name,Transform parent,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper,int size,TextAnchor align)
        {
            var r=Rect(name,parent,min,max,lower,upper); var t=r.gameObject.AddComponent<Text>();
            t.font=font; t.fontSize=size; t.color=Color.white; t.alignment=align; t.raycastTarget=false; return t;
        }
        Button Action(string caption,Transform parent,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper,UnityEngine.Events.UnityAction action)
        {
            var r=Panel(caption,parent,min,max,lower,upper,new Color(.14f,.24f,.29f,1)); var b=r.gameObject.AddComponent<Button>();
            b.targetGraphic=r.GetComponent<Image>(); b.onClick.AddListener(action);
            var colors=b.colors; colors.pressedColor=mint; b.colors=colors;
            Label("Label",r,Vector2.zero,Vector2.one,new Vector2(6,4),new Vector2(-6,-4),18,TextAnchor.MiddleCenter).text=caption; return b;
        }
        ParkingTouchButton Control(string caption,Transform parent,Vector2 min,Vector2 max,string iconResource = null)
        {
            var r=Panel(caption,parent,min,max,Vector2.zero,Vector2.zero,new Color(.13f,.20f,.24f,.94f));
            var sizing = r.gameObject.AddComponent<LayoutElement>();
            sizing.minWidth = sizing.preferredWidth = 0;
            sizing.flexibleWidth = 1;
            var button=r.gameObject.AddComponent<ParkingTouchButton>(); button.surface=r.GetComponent<Image>();
            var sprite = string.IsNullOrEmpty(iconResource) ? null : Resources.Load<Sprite>(iconResource);
            if (sprite != null)
            {
                // Reserve the bottom row for the caption, with 10 points below the icon.
                var icon = Rect("Icon",r,Vector2.zero,Vector2.one,new Vector2(12,44),new Vector2(-12,-12)).gameObject.AddComponent<Image>();
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            var label = sprite != null
                ? Label("Label",r,Vector2.zero,new Vector2(1,0),new Vector2(4,10),new Vector2(-4,34),18,TextAnchor.MiddleCenter)
                : Label("Label",r,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-4,-4),18,TextAnchor.MiddleCenter);
            label.text = caption;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 18;
            return button;
        }
        void OnDisable() { ClearControls(); }
    }
}
