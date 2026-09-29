using UnityEngine;

namespace MyGame.Player
{
    /// <summary>
    /// Feeds <see cref="PlayerController"/> state into the character's Animator (plan.md §6).
    /// Runs after <see cref="PlayerController"/> so parameters reflect this frame's movement.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [Tooltip("Movement speed at which the Walk clip plays at 1x; faster movement plays it faster.")]
        [SerializeField, Min(0.01f)] private float _walkReferenceSpeed = 1f;

        [Tooltip("Movement speed at which the Run clip plays at 1x; faster movement plays it faster.")]
        [SerializeField, Min(0.01f)] private float _runReferenceSpeed = 3f;

        private PlayerController _controller;

        private void Awake() => _controller = GetComponent<PlayerController>();

        private void OnEnable() => _controller.Jumped += OnJumped;

        private void OnDisable() => _controller.Jumped -= OnJumped;

        private void Update()
        {
            var speed = _controller.HorizontalSpeed;
            _animator.SetFloat(Parameters.Speed, speed);
            _animator.SetBool(Parameters.Running, _controller.IsRunning);
            _animator.SetBool(Parameters.Grounded, _controller.IsGrounded);
            _animator.SetFloat(Parameters.VerticalSpeed, _controller.VerticalSpeed);
            _animator.SetFloat(Parameters.WalkPlaybackSpeed, speed / _walkReferenceSpeed);
            _animator.SetFloat(Parameters.RunPlaybackSpeed, speed / _runReferenceSpeed);
        }

        private void OnJumped() => _animator.SetTrigger(Parameters.Jump);

        public static class Parameters
        {
            public const string SpeedName = "Speed";
            public const string RunningName = "Running";
            public const string GroundedName = "Grounded";
            public const string VerticalSpeedName = "VerticalSpeed";
            public const string JumpName = "Jump";
            public const string WalkPlaybackSpeedName = "WalkPlaybackSpeed";
            public const string RunPlaybackSpeedName = "RunPlaybackSpeed";

            public static readonly int Speed = Animator.StringToHash(SpeedName);
            public static readonly int Running = Animator.StringToHash(RunningName);
            public static readonly int Grounded = Animator.StringToHash(GroundedName);
            public static readonly int VerticalSpeed = Animator.StringToHash(VerticalSpeedName);
            public static readonly int Jump = Animator.StringToHash(JumpName);
            public static readonly int WalkPlaybackSpeed = Animator.StringToHash(WalkPlaybackSpeedName);
            public static readonly int RunPlaybackSpeed = Animator.StringToHash(RunPlaybackSpeedName);
        }
    }
}
