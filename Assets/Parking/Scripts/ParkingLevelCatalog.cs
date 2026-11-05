using System;
using UnityEngine;

namespace CityParking
{
    [CreateAssetMenu(menuName = "City Parking/Level Catalog")]
    public class ParkingLevelCatalog : ScriptableObject
    {
        [Min(1)] public int levelCount = 500;
        public int seed = 41721;
        public ParkingLevel[] templates;
        public int TemplateCount => templates == null ? 0 : templates.Length;
        public struct Definition
        {
            public int template, tier;
            public bool mirrored;
            public float roadOffset, spawnOffset, bayOffset, difficulty;
        }
        public Definition Describe(int index)
        {
            if (TemplateCount == 0 || index < 0 || index >= levelCount) throw new ArgumentOutOfRangeException(nameof(index));
            int tier = index / TemplateCount;
            uint state = unchecked((uint)(seed ^ (index + 1) * 73856093));
            if (state == 0) state = 1;
            float Sample() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (state & 0xffffff) / 16777215f; }
            return new Definition {
                template = index % TemplateCount, tier = tier,
                mirrored = tier > 0 && (tier % 2 == 1),
                roadOffset = tier == 0 ? 0 : (Sample() - .5f) * 1.2f,
                spawnOffset = tier == 0 ? 0 : (Sample() - .5f) * 3,
                bayOffset = tier == 0 ? 0 : (Sample() - .5f) * .16f,
                difficulty = Mathf.Clamp01(tier / (float)Mathf.Max(1, (levelCount - 1) / TemplateCount))
            };
        }
        public string Title(int index) => templates[Describe(index).template].title;
        public ParkingLevel CreateLevel(int index, Transform parent)
        {
            var d = Describe(index);
            var template = templates[d.template];
            var level = Instantiate(template, parent);
            level.name = $"Level {index + 1:000} - {template.title}";
            level.gameObject.SetActive(false);
            // Mirror positions and headings explicitly: never use negative transform scales.
            foreach (Transform child in level.transform)
            {
                Vector3 position = child.localPosition;
                if (d.mirrored)
                {
                    position.x = -position.x;
                    var angles = child.localEulerAngles; angles.y = -angles.y; child.localEulerAngles = angles;
                }
                position.z += d.roadOffset;
                child.localPosition = position;
            }
            level.spawn.localPosition += Vector3.right * d.spawnOffset;
            level.bay.localPosition += level.bay.localRotation * Vector3.right * d.bayOffset;
            level.ResizeBay(new Vector2(Mathf.Lerp(template.baySize.x, 2.45f, d.difficulty), Mathf.Lerp(template.baySize.y, 4.85f, d.difficulty)));
            level.angleTolerance = Mathf.Lerp(template.angleTolerance, 5, d.difficulty);
            level.timeLimit = Mathf.Lerp(template.timeLimit, Mathf.Max(90, template.timeLimit * .65f), d.difficulty);
            level.collisionLimit = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(template.collisionLimit, 2, d.difficulty)));
            level.gameObject.SetActive(true);
            return level;
        }
    }
}
