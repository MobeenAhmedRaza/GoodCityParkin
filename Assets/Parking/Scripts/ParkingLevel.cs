using UnityEngine;
namespace CityParking
{
    public class ParkingLevel : MonoBehaviour
    {
        public string title;
        [TextArea] public string instruction;
        public Transform spawn;
        public Transform bay;
        public Vector2 baySize;
        public float angleTolerance = 15;
        public float timeLimit = 180;
        public int collisionLimit = 6;
        public bool reverseEntry;
        public void ResizeBay(Vector2 size)
        {
            baySize = size;
            foreach (Transform child in bay)
            {
                var position = child.localPosition;
                var scale = child.localScale;
                switch (child.name)
                {
                    case "Green target surface": scale.x = size.x; scale.z = size.y; break;
                    case "Bay side": position.x = Mathf.Sign(position.x) * size.x / 2; scale.z = size.y; break;
                    case "Bay end": position.z = Mathf.Sign(position.z) * size.y / 2; scale.x = size.x; break;
                }
                child.localPosition = position; child.localScale = scale;
            }
        }
    }
}
