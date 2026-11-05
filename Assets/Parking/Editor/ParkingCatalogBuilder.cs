using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CityParking;

public static class ParkingCatalogBuilder
{
    public const string CatalogPath = "Assets/Parking/LevelCatalog.asset";
    const string PrefabFolder = "Assets/Parking/Prefabs";
    [MenuItem("Tools/City Parking/Upgrade to 500 Generated Levels")]
    public static void Upgrade()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SuburbanParking.unity");
        Convert(UnityEngine.Object.FindFirstObjectByType<ParkingCourse>());
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CITY_PARKING_500_READY");
    }
    public static void Convert(ParkingCourse course)
    {
        if (course.catalog != null && (course.levels == null || course.levels.Length == 0)) return;
        if (course.levels == null || course.levels.Length != 10) throw new InvalidOperationException("Migration requires the original ten level roots.");
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();
        var catalog = AssetDatabase.LoadAssetAtPath<ParkingLevelCatalog>(CatalogPath);
        if (!catalog) { catalog = ScriptableObject.CreateInstance<ParkingLevelCatalog>(); AssetDatabase.CreateAsset(catalog,CatalogPath); }
        catalog.levelCount = 500; catalog.templates = new ParkingLevel[10];
        var neighborhood = new GameObject("Shared Neighborhood");
        foreach (Transform child in course.levels[0].transform)
            if (EnvironmentObject(child.name))
            {
                var copy = UnityEngine.Object.Instantiate(child.gameObject,neighborhood.transform);
                copy.name = child.name;
            }
        var environmentPrefab = PrefabUtility.SaveAsPrefabAsset(neighborhood,PrefabFolder+"/Neighborhood.prefab");
        UnityEngine.Object.DestroyImmediate(neighborhood);
        for (int i=0;i<10;i++)
        {
            var template = UnityEngine.Object.Instantiate(course.levels[i]);
            template.name = $"Layout {i+1:00} - {template.title}";
            foreach (Transform child in template.transform.Cast<Transform>().ToArray())
                if (EnvironmentObject(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            template.gameObject.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(template.gameObject,$"{PrefabFolder}/Layout{i+1:00}.prefab");
            catalog.templates[i] = prefab.GetComponent<ParkingLevel>();
            UnityEngine.Object.DestroyImmediate(template.gameObject);
        }
        EditorUtility.SetDirty(catalog);
        PrefabUtility.InstantiatePrefab(environmentPrefab);
        foreach(var level in course.levels) UnityEngine.Object.DestroyImmediate(level.gameObject);
        course.levels = Array.Empty<ParkingLevel>();
        course.catalog = catalog;
        course.name = "Parking Course - 500 Generated Levels";
        EditorUtility.SetDirty(course);
        AssetDatabase.SaveAssets();
    }
    static bool EnvironmentObject(string name)
    {
        return name.StartsWith("building-type-") || name == "tree-small" || name == "driveway-short" || name == "fence"
            || name == "Neighborhood lawn" || name == "Practice street" || name == "Raised sidewalk"
            || name == "End curb" || name == "Start curb" || name == "Road edge dash";
    }
}
