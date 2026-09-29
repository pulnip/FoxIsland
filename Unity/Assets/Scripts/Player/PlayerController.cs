using System;
using MyGame.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame.Player
{
    /// <summary>
    /// Camera-relative walking and jumping on a <see cref="CharacterController"/> (plan.md §4.5, §6).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private const float GroundedVerticalSpeed = -2f;

        [SerializeField] private float _walkSpeed = 1f;
        [SerializeField] private float _runSpeed = 3f;
        [SerializeField] private float _turnSpeed = 720f;
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _gravity = -25f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpBufferTime = 0.1f;
        [SerializeField] private Transform _cameraTransform;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _toggleWalkAction;
        private float _verticalSpeed;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastJumpPressedTime = float.NegativeInfinity;

        public event Action Jumped;

        /// <summary>
        /// When false, move and jump input are ignored; gravity still applies.
        /// </summary>
        public bool InputEnabled { get; set; } = true;

        public bool IsGrounded => _controller.isGrounded;

        public float VerticalSpeed => _verticalSpeed;

        public float HorizontalSpeed { get; private set; }

        /// <summary>
        /// Running is the default; the toggle-walk key switches between walking and running.
        /// </summary>
        public bool IsRunning { get; private set; } = true;

        private void Awake() => _controller = GetComponent<CharacterController>();

        private void OnEnable()
        {
            var actions = InputSystem.actions;
            _moveAction = actions.FindAction("Player/Move", throwIfNotFound: true);
            _jumpAction = actions.FindAction("Player/Jump", throwIfNotFound: true);
            _toggleWalkAction = actions.FindAction("Player/ToggleWalk", throwIfNotFound: true);
            _moveAction.Enable();
            _jumpAction.Enable();
            _toggleWalkAction.Enable();
        }

        private void Start()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            var now = Time.time;
            if (_controller.isGrounded)
            {
                _lastGroundedTime = now;
                if (_verticalSpeed < 0f)
                {
                    _verticalSpeed = GroundedVerticalSpeed;
                }
            }

            if (InputEnabled && _jumpAction.WasPressedThisFrame())
            {
                _lastJumpPressedTime = now;
            }

            if (now - _lastGroundedTime <= _coyoteTime && now - _lastJumpPressedTime <= _jumpBufferTime)
            {
                _verticalSpeed = Mathf.Sqrt(2f * _jumpHeight * -_gravity);
                _lastGroundedTime = float.NegativeInfinity;
                _lastJumpPressedTime = float.NegativeInfinity;
                Jumped?.Invoke();
            }

            _verticalSpeed += _gravity * Time.deltaTime;

            var input = InputEnabled ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (InputEnabled && _toggleWalkAction.WasPressedThisFrame())
            {
                IsRunning = !IsRunning;
            }

            var horizontal = ToWorldDirection(input) * (IsRunning ? _runSpeed : _walkSpeed);
            var flags = _controller.Move((horizontal + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f)
            {
                _verticalSpeed = 0f;
            }

            HorizontalSpeed = horizontal.magnitude;
            FaceTowards(horizontal);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f && hit.collider.TryGetComponent<CrumblingTile>(out var tile))
            {
                tile.NotifyStoodOn();
            }
        }

        private Vector3 ToWorldDirection(Vector2 input)
        {
            if (_cameraTransform == null)
            {
                return new Vector3(input.x, 0f, input.y);
            }

            var forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        private void FaceTowards(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);
        }
    }
}
