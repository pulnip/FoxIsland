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

        [Tooltip("On ice, how fast the fox gains speed toward the input (units/s²).")]
        [SerializeField] private float _iceAcceleration = 3f;

        [Tooltip("On ice with no input, how fast a slide dies down (units/s²).")]
        [SerializeField] private float _iceDeceleration = 2f;

        [SerializeField] private Transform _cameraTransform;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _toggleWalkAction;
        private float _verticalSpeed;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastJumpPressedTime = float.NegativeInfinity;
        private Vector3 _horizontalVelocity;
        private bool _groundIsIce;
        private bool _keepsSlideInAir;
        private bool _wasGrounded = true;

        public event Action Jumped;

        /// <summary>Raised on the frame the player touches down after being airborne.</summary>
        public event Action Landed;

        public event Action<bool> RunningChanged;

        /// <summary>
        /// When false, move and jump input are ignored; gravity still applies.
        /// </summary>
        public bool InputEnabled { get; set; } = true;

        public bool IsGrounded => _controller.isGrounded;

        public float VerticalSpeed => _verticalSpeed;

        public float JumpHeight => _jumpHeight;

        /// <summary>
        /// The speed the player is asking for, not the actual velocity: a fox sliding on ice with
        /// no input reads 0, so it slides in its idle pose.
        /// </summary>
        public float HorizontalSpeed { get; private set; }

        /// <summary>Standing on an ice tile (plan.md §4.9): movement keeps momentum.</summary>
        public bool IsOnIce => _controller.isGrounded && _groundIsIce;

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
                _keepsSlideInAir = IsOnIce;
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
                RunningChanged?.Invoke(IsRunning);
            }

            var target = ToWorldDirection(input) * (IsRunning ? _runSpeed : _walkSpeed);
            var sliding = IsOnIce || (!_controller.isGrounded && _keepsSlideInAir);
            if (sliding)
            {
                var rate = target.sqrMagnitude > 0.0001f ? _iceAcceleration : _iceDeceleration;
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, rate * Time.deltaTime);
            }
            else
            {
                _horizontalVelocity = target;
            }

            var flags = _controller.Move((_horizontalVelocity + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f)
            {
                _verticalSpeed = 0f;
            }

            if (sliding && (flags & CollisionFlags.Sides) != 0)
            {
                // A wall stops the slide instead of the fox pushing into it forever.
                var actual = _controller.velocity;
                _horizontalVelocity = new Vector3(actual.x, 0f, actual.z);
            }

            var grounded = _controller.isGrounded;
            if (grounded && !_wasGrounded)
            {
                _keepsSlideInAir = false;
                Landed?.Invoke();
            }

            _wasGrounded = grounded;
            HorizontalSpeed = target.magnitude;
            FaceTowards(target);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f)
            {
                _groundIsIce = hit.collider.TryGetComponent<Tile>(out var ground) && ground.Spec.IsIce;
            }

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
