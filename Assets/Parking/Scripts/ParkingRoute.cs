using System.Collections.Generic;
using UnityEngine;

namespace CityParking
{
    // A visual approach guide, independent of the required final vehicle heading.
    public static class ParkingRoute
    {
        const int Width = 61, Height = 77;
        const float Step = .5f, Clearance = 1f, Spacing = 2.5f;

        public static List<Vector3> FindPath(ParkingLevel level)
        {
            Physics.SyncTransforms();
            var obstacles = new List<Bounds>();
            foreach (var collider in level.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || !collider.CompareTag("Respawn")) continue;
                var bounds = collider.bounds;
                bounds.Expand(new Vector3(Clearance * 2, 0, Clearance * 2));
                obstacles.Add(bounds);
            }
            bool Clear(Vector3 a, Vector3 b)
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .1f));
                for (int i = 0; i <= samples; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)samples);
                    foreach (var bounds in obstacles)
                        if (p.x > bounds.min.x && p.x < bounds.max.x && p.z > bounds.min.z && p.z < bounds.max.z)
                            return false;
                }
                return true;
            }
            Vector3 Point(int cell) => new Vector3(-15 + cell % Width * Step, .09f, -19 + cell / Width * Step);
            int Cell(Vector3 p) => Mathf.Clamp(Mathf.RoundToInt((p.z + 19) / Step), 0, Height - 1) * Width
                + Mathf.Clamp(Mathf.RoundToInt((p.x + 15) / Step), 0, Width - 1);
            var blocked = new bool[Width * Height];
            var previous = new int[blocked.Length];
            for (int i = 0; i < blocked.Length; i++) { blocked[i] = !Clear(Point(i), Point(i)); previous[i] = -1; }
            Vector3 startPoint = level.spawn.position, goalPoint = level.bay.position;
            startPoint.y = goalPoint.y = .09f;
            int start = Cell(startPoint), goal = Cell(goalPoint);
            var result = new List<Vector3>();
            if (blocked[start] || blocked[goal] || !Clear(startPoint, Point(start)) || !Clear(Point(goal), goalPoint)) return result;
            var queue = new Queue<int>();
            queue.Enqueue(start); previous[start] = start;
            while (queue.Count > 0 && previous[goal] < 0)
            {
                int cell = queue.Dequeue(), x = cell % Width, z = cell / Width;
                void Add(int next)
                {
                    if (blocked[next] || previous[next] >= 0 || !Clear(Point(cell), Point(next))) return;
                    previous[next] = cell; queue.Enqueue(next);
                }
                if (z < Height - 1) Add(cell + Width);
                if (x > 0) Add(cell - 1);
                if (x < Width - 1) Add(cell + 1);
                if (z > 0) Add(cell - Width);
            }
            if (previous[goal] < 0) return result;
            result.Add(goalPoint);
            for (int cell = goal; cell != start; cell = previous[cell]) result.Add(Point(cell));
            result.Add(Point(start)); result.Add(startPoint); result.Reverse();
            // Remove grid stair-steps only when the shortcut still clears obstacles.
            var smooth = new List<Vector3> { startPoint };
            for (int i = 0; i < result.Count - 1;)
            {
                int next = result.Count - 1;
                while (next > i + 1 && !Clear(result[i], result[next])) next--;
                smooth.Add(result[next]); i = next;
            }
            return smooth;
        }

        public static void Build(ParkingLevel level)
        {
            if (level.transform.Find("Route arrows") != null) return;
            var sprite = Resources.Load<Sprite>("front");
            if (sprite == null) { Debug.LogWarning("Missing Resources/front sprite", level); return; }
            var path = FindPath(level);
            if (path.Count < 2) { Debug.LogWarning("No clear parking route for " + level.name, level); return; }
            var root = new GameObject("Route arrows").transform;
            root.SetParent(level.transform, false);
            float distanceToNext = Spacing;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 segment = path[i] - path[i - 1];
                float length = segment.magnitude;
                if (length < .001f) continue;
                Vector3 direction = segment / length;
                while (distanceToNext < length)
                {
                    Vector3 position = path[i - 1] + direction * distanceToNext;
                    // Keep the bay's final-heading marking legible.
                    Vector3 local = level.bay.InverseTransformPoint(position);
                    if (Mathf.Abs(local.x) > level.baySize.x / 2 || Mathf.Abs(local.z) > level.baySize.y / 2)
                    {
                        var arrow = new GameObject("Front route marker").AddComponent<SpriteRenderer>();
                        arrow.transform.SetParent(root, false);
                        arrow.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.down, direction));
                        arrow.transform.localScale = Vector3.one * (1.6f / sprite.bounds.size.y);
                        arrow.sprite = sprite;
                        arrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        arrow.receiveShadows = false;
                    }
                    distanceToNext += Spacing;
                }
                distanceToNext -= length;
            }
        }
    }
}
