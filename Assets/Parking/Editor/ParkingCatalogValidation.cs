using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CityParking;

public static class ParkingCatalogValidation
{
    [MenuItem("Tools/City Parking/Validate All Generated Levels")]
    public static void Run()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ParkingLevelCatalog>(ParkingCatalogBuilder.CatalogPath);
        if (!catalog || catalog.levelCount != 500 || catalog.TemplateCount != 10) throw new Exception("Invalid 500-level catalog");
        var fingerprints = new HashSet<string>();
        var probe = new GameObject("Validation car footprint").AddComponent<BoxCollider>();
        probe.size = new Vector3(1.8f,1.3f,3.8f); probe.center = new Vector3(0,.65f,0);
        try
        {
            for(int i=0;i<catalog.levelCount;i++)
            {
                var d=catalog.Describe(i);
                if (!d.Equals(catalog.Describe(i))) throw new Exception("Non-deterministic level " + (i+1));
                var level=catalog.CreateLevel(i,null);
                try
                {
                    string fingerprint=JsonUtility.ToJson(d);
                    if (!fingerprints.Add(fingerprint)) throw new Exception("Duplicate definition " + (i+1));
                    if(level.baySize.x < 2.4f || level.baySize.y < 4.8f || level.timeLimit < 90 || level.collisionLimit < 2) throw new Exception("Invalid difficulty limits " + (i+1));
                    if(level.bay.lossyScale != Vector3.one) throw new Exception("Scaled bay breaks containment checks");
                    Physics.SyncTransforms();
                    var obstacles=level.GetComponentsInChildren<BoxCollider>().Where(c=>c.enabled && !c.isTrigger).ToArray();
                    foreach(var marker in new[]{level.spawn,level.bay})
                        foreach(var obstacle in obstacles)
                            if(Physics.ComputePenetration(probe,marker.position+Vector3.up*.04f,marker.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float distance) && distance > .01f)
                                throw new Exception($"Level {i+1}: {marker.name} overlaps {obstacle.name}");
                    if(!Reachable(level,obstacles)) throw new Exception($"Level {i+1}: approach blocked");
                }
                finally { UnityEngine.Object.DestroyImmediate(level.gameObject); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(probe.gameObject); }
        File.WriteAllText("/tmp/city-parking-catalog-validation.txt", "PASS: 500 deterministic unique definitions; valid limits; clear spawn and parked-car footprints; connected approach corridors. This is not a steering feasibility proof.\n");
        Debug.Log("CITY_PARKING_500_VALIDATED");
    }
    static bool Reachable(ParkingLevel level,BoxCollider[] obstacles)
    {
        // Necessary route check using a one-metre half-width. Driving manoeuvres still need playtesting.
        const int width=61, height=77;
        var blocked=new bool[width*height]; var seen=new bool[width*height];
        var bounds=obstacles.Select(c=>c.bounds).ToArray();
        for(int z=0;z<height;z++) for(int x=0;x<width;x++)
        {
            float px=-15+x*.5f,pz=-19+z*.5f;
            foreach(var b in bounds)
                if(px>b.min.x-1 && px<b.max.x+1 && pz>b.min.z-1 && pz<b.max.z+1) { blocked[z*width+x]=true; break; }
        }
        int Cell(Vector3 p) => Mathf.Clamp(Mathf.RoundToInt((p.z+19)*2),0,height-1)*width+Mathf.Clamp(Mathf.RoundToInt((p.x+15)*2),0,width-1);
        int start=Cell(level.spawn.position),goal=Cell(level.bay.position);
        if(blocked[start] || blocked[goal]) return false;
        var queue=new Queue<int>(); queue.Enqueue(start); seen[start]=true;
        while(queue.Count>0)
        {
            int cell=queue.Dequeue(); if(cell==goal)return true;
            int x=cell%width,z=cell/width;
            void Add(int next) { if(!blocked[next] && !seen[next]) { seen[next]=true; queue.Enqueue(next); } }
            if(x>0)Add(cell-1); if(x<width-1)Add(cell+1); if(z>0)Add(cell-width); if(z<height-1)Add(cell+width);
        }
        return false;
    }
}
