using System;
using MyGame.Map;
using MyGame.Puzzle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame.Player
{
    /// <summary>
    /// Camera-relative walking and jumping on a <see cref="CharacterController"/> (plan.md §4.5, §6).
    /// Rides whatever it stands on (rising blocks, platforms, crates, icebergs) and pushes crates.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private const float GroundedVerticalSpeed = -2f;
        private const float PushAlignment = 0.7f;
        private const float PushFootTolerance = 0.15f;

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

        [Tooltip("Speed multiplier while wading in shallow water.")]
        [SerializeField, Range(0.1f, 1f)] private float _waterSpeedFactor = 0.6f;

        [SerializeField] private Transform _cameraTransform;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _toggleWalkAction;
        private float _verticalSpeed;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _lastJumpPressedTime = float.NegativeInfinity;
        private Vector3 _horizontalVelocity;
        private CellSpec _ground;
        private bool _keepsSlideInAir;
        private bool _wasGrounded = true;
        private Transform _groundBody;
        private Vector3 _groundBodyPosition;

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

        /// <summary>Standing on ice or an iceberg (plan.md §4.9): movement keeps momentum.</summary>
        public bool IsOnIce => _controller.isGrounded && _ground.IsSlippery;

        /// <summary>Standing in shallow water: slower going.</summary>
        public bool IsWading => _controller.isGrounded && _ground.IsBlock && _ground.Skin == Skin.Water;

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
            RideGround();
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

            var target = ToWorldDirection(input) * (IsRunning ? _runSpeed : _walkSpeed) * (IsWading ? _waterSpeedFactor : 1f);
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
            if (grounded && _groundBody != null)
            {
                _groundBodyPosition = _groundBody.position;
            }
            else
            {
                _groundBody = null;
            }

            if (grounded && !_wasGrounded)
            {
                _keepsSlideInAir = false;
                Landed?.Invoke();
            }

            _wasGrounded = grounded;
            HorizontalSpeed = target.magnitude;
            FaceTowards(target);
        }

        /// <summary>Throws the fox up <paramref name="height"/> units, like a jump it did not ask for.</summary>
        public void Launch(float height)
        {
            _verticalSpeed = Mathf.Sqrt(2f * height * -_gravity);
            _lastGroundedTime = float.NegativeInfinity;
            _lastJumpPressedTime = float.NegativeInfinity;
            _keepsSlideInAir = IsOnIce;
            Jumped?.Invoke();
        }

        /// <summary>
        /// Moves along with the body stood on last frame, then syncs physics so the move below sees
        /// colliders where their transforms are now.
        /// </summary>
        private void RideGround()
        {
            if (_groundBody != null)
            {
                var delta = _groundBody.position - _groundBodyPosition;
                if (delta.sqrMagnitude > 0f)
                {
                    transform.position += delta;
                }
            }

            Physics.SyncTransforms();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f)
            {
                _ground = hit.collider.TryGetComponent<Tile>(out var ground) ? ground.Spec : CellSpec.Hole;
                _groundBody = hit.collider.transform;
            }

            if (hit.normal.y > 0.5f && hit.collider.TryGetComponent<CrumblingTile>(out var tile))
            {
                tile.NotifyStoodOn();
            }

            if (Mathf.Abs(hit.normal.y) < 0.3f && hit.collider.TryGetComponent<Crate>(out var crate))
            {
                TryPush(crate, hit);
            }
        }

        /// <summary>Walking into a crate's side, along a grid axis and from its floor, pushes it.</summary>
        private void TryPush(Crate crate, ControllerColliderHit hit)
        {
            var moving = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (!InputEnabled || !_wasGrounded || moving.sqrMagnitude < 0.0001f || transform.position.y < crate.Bottom - PushFootTolerance)
            {
                return;
            }

            var away = -hit.normal;
            var direction = Mathf.Abs(away.x) > Mathf.Abs(away.z)
                ? new Vector2Int(away.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, away.z > 0f ? 1 : -1);
            if (Vector3.Dot(moving.normalized, new Vector3(direction.x, 0f, direction.y)) > PushAlignment)
            {
                crate.Push(direction);
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
