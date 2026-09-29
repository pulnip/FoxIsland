using UnityEngine;

namespace MyGame.CameraRig
{
    /// <summary>
    /// Fixed orthographic quarter-view camera that keeps <see cref="FramedBounds"/> fully on screen.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private float _pitch = 30f;
        [SerializeField] private float _yaw = 45f;
        [SerializeField] private float _distance = 20f;
        [SerializeField, Range(0.1f, 1f)] private float _screenFill = 0.7f;
        [SerializeField] private Bounds _framedBounds = new(new Vector3(1.5f, 0.85f, 1.5f), new Vector3(4f, 2.7f, 4f));

        private Camera _camera;
        private bool _isDirty = true;
        private float _appliedAspect;

        public Bounds FramedBounds
        {
            get => _framedBounds;
            set
            {
                _framedBounds = value;
                _isDirty = true;
            }
        }

        private Camera Camera => _camera != null ? _camera : _camera = GetComponent<Camera>();

        private void OnEnable() => _isDirty = true;

        private void OnValidate() => _isDirty = true;

        private void LateUpdate()
        {
            if (_isDirty || !Mathf.Approximately(Camera.aspect, _appliedAspect))
            {
                Apply();
            }
        }

        public void Apply()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(_framedBounds.center - rotation * Vector3.forward * _distance, rotation);

            var extents = _framedBounds.extents;
            var toView = Quaternion.Inverse(rotation);
            var halfWidth = 0f;
            var halfHeight = 0f;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                var viewCorner = toView * corner;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(viewCorner.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(viewCorner.y));
            }

            const float clipMargin = 10f;
            var camera = Camera;
            var radius = extents.magnitude;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / camera.aspect) / _screenFill;
            camera.nearClipPlane = Mathf.Max(0.01f, _distance - radius - clipMargin);
            camera.farClipPlane = _distance + radius + clipMargin;

            _appliedAspect = camera.aspect;
            _isDirty = false;
        }
    }
}
