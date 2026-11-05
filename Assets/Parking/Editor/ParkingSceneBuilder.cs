using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CityParking;

public static class ParkingSceneBuilder
{
    const string Output = "Assets/Scenes/SuburbanParking.unity";
    const string Suburb = "Assets/kenney_city-kit-suburban_20/Models/FBX format/";
    const string Cars = "Assets/kenney_car-kit/Models/FBX format/";
    static Material asphalt, grass, white, green, curb, cityMaterial, carMaterial;
    [MenuItem("Tools/City Parking/Build 500 Level Scene")]
    public static void Build()
    {
        if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            Directory.CreateDirectory("Assets/Parking/Materials");
            asphalt = Material("Asphalt", new Color(.16f,.20f,.23f));
            grass = Material("Grass", new Color(.39f,.61f,.38f));
            white = Material("Markings", new Color(.96f,.93f,.78f));
            green = Material("Parking Green", new Color(.20f,.65f,.47f));
            curb = Material("Concrete", new Color(.71f,.73f,.69f));
            cityMaterial = Material("Suburban Atlas", Color.white, Suburb + "Textures/colormap.png");
            carMaterial = Material("Car Atlas", Color.white, Cars + "Textures/colormap.png");
            var light = new GameObject("Warm afternoon sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f;
            light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(50,-35,0);
            RenderSettings.ambientLight = new Color(.65f,.72f,.8f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 33;
            camera.transform.rotation = Quaternion.Euler(ParkingCamera.IsometricPitch,45,0);
            camera.transform.position = -camera.transform.forward * 70;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.65f,.79f,.84f);
            camera.farClipPlane = 200; camera.gameObject.AddComponent<AudioListener>();
            var course = new GameObject("Parking Course - 10 Levels").AddComponent<ParkingCourse>();
            course.overview = camera;
            var player = new GameObject("Player Car");
            var body = player.AddComponent<Rigidbody>(); body.mass = 1200; body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var collider = player.AddComponent<BoxCollider>(); collider.center = new Vector3(0,.65f,0); collider.size = new Vector3(1.8f,1.3f,3.8f);
            Model(Cars, "sedan", player.transform, Vector3.zero, new Vector3(1.8f,1.3f,3.8f), 0, false);
            course.car = player.AddComponent<ParkingCar>(); course.car.course = course;
            course.levels = new ParkingLevel[10];
            string[] titles = { "First Stop", "Driveway Arrival", "Right Angle", "Reverse Bay", "Garden Slalom", "Narrow Gate", "Parallel Park", "Offset Alley", "Tight Reverse", "Final Precision" };
            string[] tips = { "Drive straight into the wide practice bay.", "Turn into the driveway between the planters.", "Approach from the street and make a 90 degree turn.", "Pass the bay, then reverse into it facing the arrow.", "Weave between the garden islands, then park.", "Line up before entering the narrow garden gate.", "Park between two cars beside the curb.", "Negotiate two offset gates before the final turn.", "Reverse between the neighboring cars with little clearance.", "Clear the slalom and gate, then reverse into the final bay." };
            for (int i = 0; i < 10; i++)
            {
                var root = new GameObject($"Level {i + 1:00} - {titles[i]}");
                var level = root.AddComponent<ParkingLevel>(); course.levels[i] = level;
                level.title = titles[i]; level.instruction = tips[i];
                level.timeLimit = 210 - i * 10; level.collisionLimit = Mathf.Max(2, 7 - i / 2);
                level.angleTolerance = 20 - i * 1.5f;
                level.baySize = new Vector2(4.8f - i * .22f, 7 - i * .17f);
                level.spawn = Marker(root.transform, "Start", new Vector3(0,.04f,-16), 0);
                Vector3 target = new Vector3(0,0,13); float yaw = 0;
                if (i == 1) target.x = 7;
                if (i == 2 || i == 7) { target = new Vector3(9,0,10); yaw = 90; }
                if (i == 3 || i == 8 || i == 9) { target = new Vector3(5,0,12); yaw = 180; level.reverseEntry = true; }
                if (i == 6) { target = new Vector3(11,0,5); level.baySize = new Vector2(3.35f,6.3f); }
                level.bay = Marker(root.transform, "Parking Bay - arrow is required heading", target, yaw);
                Ground(root.transform, i);
                Bay(level);
                switch(i)
                {
                    case 1:
                        Island(root.transform, 3, 10, 1.2f, 7); Island(root.transform, 11,10,1.2f,7); break;
                    case 2:
                        Island(root.transform, 12, 3, 5, 1); Island(root.transform, 12,17,5,1); break;
                    case 3:
                        Parked(root.transform, 0,12,180); Parked(root.transform,10,12,180); break;
                    case 4:
                        Island(root.transform,-9,-6,14,1.5f); Island(root.transform,9,3,14,1.5f); break;
                    case 5:
                        Island(root.transform,-9,3,14.4f,1.5f); Island(root.transform,9,3,14.4f,1.5f);
                        Parked(root.transform,-4,13,0); Parked(root.transform,4,13,0); break;
                    case 6:
                        Parked(root.transform,11,-1.3f,0); Parked(root.transform,11,11.3f,0);
                        Box("Curb behind parallel bay",root.transform,new Vector3(13,.2f,5),new Vector3(.4f,.4f,19),curb,true); break;
                    case 7:
                        Island(root.transform,-8,-6,16,1.2f); Island(root.transform,11,-6,10,1.2f);
                        Island(root.transform,-11,3,10,1.2f); Island(root.transform,8,3,16,1.2f); break;
                    case 8:
                        Parked(root.transform,1.6f,12,180); Parked(root.transform,8.4f,12,180);
                        Island(root.transform,5,17,9,1); Island(root.transform,-9,-2,14,1.2f); break;
                    case 9:
                        Island(root.transform,-9,-8,14,1.2f); Island(root.transform,9,-1,14,1.2f);
                        Island(root.transform,-8,6,16,1.2f); Island(root.transform,11.3f,6,9.4f,1.2f);
                        Parked(root.transform,1.8f,12,180); Parked(root.transform,8.2f,12,180);
                        Island(root.transform,5,17,9,1); break;
                }
                root.SetActive(i == 0);
            }
            player.transform.SetPositionAndRotation(course.levels[0].spawn.position, course.levels[0].spawn.rotation);
            ConfigureMobile(course);
            ParkingCatalogBuilder.Convert(course);
            EditorSceneManager.SaveScene(scene, Output);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != Output).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(Output, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CITY_PARKING_BUILD_OK: 500 generated levels saved to " + Output);
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
    [MenuItem("Tools/City Parking/Upgrade Isometric Mobile Camera and UI")]
    public static void UpgradeMobile()
    {
        var scene = EditorSceneManager.OpenScene(Output);
        var course = UnityEngine.Object.FindFirstObjectByType<ParkingCourse>();
        ConfigureMobile(course);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CITY_PARKING_MOBILE_OK");
    }
    static void ConfigureMobile(ParkingCourse course)
    {
        var rig = course.overview.GetComponent<ParkingCamera>();
        if (!rig) rig = course.overview.gameObject.AddComponent<ParkingCamera>();
        rig.target = course.car.transform;
        rig.drivingSize = 14;
        rig.introDuration = 1.5f;
        course.overview.orthographic = true;
        course.overview.orthographicSize = 14;
        course.overview.transform.rotation = rig.ViewRotation;
        course.overview.transform.position = course.car.transform.position - rig.ViewRotation * Vector3.up * 1.2f - course.overview.transform.forward * 70;
        var hud = course.GetComponent<ParkingMobileHUD>();
        if (!hud) hud = course.gameObject.AddComponent<ParkingMobileHUD>();
        hud.course = course; hud.cameraRig = rig;
        course.car.mobileControls = hud;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
    }
    static Material Material(string name, Color color, string texture = null)
    {
        string path = "Assets/Parking/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
        mat.color = color; mat.SetFloat("_Smoothness", .12f);
        if (texture != null) mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
        EditorUtility.SetDirty(mat); return mat;
    }
    static Transform Marker(Transform parent, string name, Vector3 position, float yaw)
    {
        var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition = position; t.localRotation = Quaternion.Euler(0,yaw,0); return t;
    }
    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat, bool obstacle = false, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent,false);
        go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        if (obstacle) go.tag = "Respawn";
        return go;
    }
    static GameObject Model(string folder, string name, Transform parent, Vector3 position, Vector3 size, float yaw = 0, bool collision = true)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".fbx");
        if (!asset) throw new InvalidOperationException("Missing model: " + name);
        var wrapper = new GameObject(name); wrapper.transform.SetParent(parent,false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset); model.transform.SetParent(wrapper.transform,false);
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds; foreach (var r in renderers) { bounds.Encapsulate(r.bounds); r.sharedMaterials = r.sharedMaterials.Select(_ => folder == Cars ? carMaterial : cityMaterial).ToArray(); }
        model.transform.localScale = new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
        model.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),model.transform.localScale);
        if (collision) { var box = wrapper.AddComponent<BoxCollider>(); box.center = new Vector3(0,size.y/2,0); box.size = size; wrapper.tag = "Respawn"; }
        wrapper.transform.localPosition = position; wrapper.transform.localRotation = Quaternion.Euler(0,yaw,0); return wrapper;
    }
    static void Ground(Transform root, int level)
    {
        Box("Neighborhood lawn",root,new Vector3(0,-.25f,0),new Vector3(64,.4f,60),grass);
        Box("Practice street",root,new Vector3(0,-.05f,0),new Vector3(32,.1f,42),asphalt);
        for (int side = -1; side <= 1; side += 2)
        {
            Box("Raised sidewalk",root,new Vector3(side*17,.1f,0),new Vector3(2,.2f,44),curb,true);
            for (int j = 0; j < 4; j++)
            {
                float z = -16+j*11;
                Model(Suburb,"building-type-"+(char)('a'+(level+j*3)%21),root,new Vector3(side*24,0,z),new Vector3(8,5+(j%2)*2,7),side == -1 ? 90 : -90);
                Model(Suburb,"tree-small",root,new Vector3(side*19.5f,0,z-4),new Vector3(2.3f,4,2.3f));
                Model(Suburb,"driveway-short",root,new Vector3(side*20,0,z+1),new Vector3(4,.06f,3),0,false);
                Model(Suburb,"fence",root,new Vector3(side*24,0,z-4.4f),new Vector3(8,1, .15f));
            }
        }
        Box("End curb",root,new Vector3(0,.2f,21),new Vector3(34,.4f,.4f),curb,true);
        Box("Start curb",root,new Vector3(0,.2f,-21),new Vector3(34,.4f,.4f),curb,true);
        for (int j=0;j<8;j++) Box("Road edge dash",root,new Vector3(-14,.015f,-17+j*5),new Vector3(.12f,.025f,2),white,false,false);
    }
    static void Island(Transform root,float x,float z,float width,float depth)
    {
        Box("Garden island curb",root,new Vector3(x,.23f,z),new Vector3(width,.46f,depth),curb,true);
        Box("Garden island",root,new Vector3(x,.47f,z),new Vector3(width-.15f,.05f,depth-.15f),grass,false,false);
        int count = Mathf.Max(1,Mathf.FloorToInt(width/3));
        for (int n=0;n<count;n++) Model(Suburb,"planter",root,new Vector3(x+(n-(count-1)*.5f)*2.6f,.49f,z),new Vector3(Mathf.Min(1,width*.7f),.65f,Mathf.Min(.7f,depth*.7f)));
    }
    static void Parked(Transform root,float x,float z,float yaw) { Model(Cars,"hatchback-sports",root,new Vector3(x,.02f,z),new Vector3(1.9f,1.4f,4),yaw); }
    static void Bay(ParkingLevel level)
    {
        var t = level.bay; float w = level.baySize.x, l = level.baySize.y;
        Box("Green target surface",t,new Vector3(0,.018f,0),new Vector3(w,.025f,l),green,false,false);
        foreach (int side in new[]{-1,1})
        {
            Box("Bay side",t,new Vector3(side*w/2,.04f,0),new Vector3(.12f,.02f,l),white,false,false);
            Box("Bay end",t,new Vector3(0,.04f,side*l/2),new Vector3(w,.02f,.12f),white,false,false);
        }
        Box("Direction arrow stem",t,new Vector3(0,.055f,.5f),new Vector3(.15f,.02f,1.2f),white,false,false);
        foreach(int side in new[]{-1,1})
        {
            var arm = Box("Direction arrow head",t,new Vector3(side*.22f,.055f,.88f),new Vector3(.13f,.02f,.7f),white,false,false);
            arm.transform.localRotation = Quaternion.Euler(0,side*-40,0);
        }
    }
}
