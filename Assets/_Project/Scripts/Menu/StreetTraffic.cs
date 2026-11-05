using UnityEngine;

namespace CommunityHelpers
{
    /// <summary>
    /// Sends cars slowly along the street outside the window, right to left (local -X), at random intervals.
    /// Cars are a small pool of inactive children; one is reused each time. All cars drive at the same speed,
    /// so they never catch up with each other.
    /// </summary>
    public class StreetTraffic : MonoBehaviour
    {
        [SerializeField] private Transform[] cars;
        [Tooltip("Where cars appear (right) and disappear (left), in this object's local X.")]
        [SerializeField] private float startX = 24f;
        [SerializeField] private float endX = -24f;
        [SerializeField] private float speed = 2.6f;               // m/s, slow and calm
        [SerializeField] private Vector2 secondsBetweenCars = new(3f, 9f);
        [Tooltip("Never spawn a car closer than this behind the previous one.")]
        [SerializeField] private float minGap = 7f;

        private float _next;
        private Transform _last;

        private void Start()
        {
            foreach (var c in cars) if (c != null) c.gameObject.SetActive(false);
            _next = Time.time + Random.Range(0.5f, 2.5f);
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            foreach (var c in cars)
            {
                if (c == null || !c.gameObject.activeSelf) continue;
                var p = c.localPosition;
                p.x -= step;
                c.localPosition = p;
                if (p.x < endX) c.gameObject.SetActive(false);
            }

            if (Time.time < _next) return;
            if (_last != null && _last.gameObject.activeSelf && startX - _last.localPosition.x < minGap) return;

            var car = PickIdleCar();
            if (car != null)
            {
                var p = car.localPosition;
                car.localPosition = new Vector3(startX, p.y, p.z);
                car.gameObject.SetActive(true);
                _last = car;
            }
            _next = Time.time + Random.Range(secondsBetweenCars.x, secondsBetweenCars.y);
        }

        private Transform PickIdleCar()
        {
            int idle = 0;
            foreach (var c in cars) if (c != null && !c.gameObject.activeSelf) idle++;
            if (idle == 0) return null;
            int pick = Random.Range(0, idle);
            foreach (var c in cars)
            {
                if (c == null || c.gameObject.activeSelf) continue;
                if (pick-- == 0) return c;
            }
            return null;
        }
    }
}
