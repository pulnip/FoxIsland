using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Parameters = MyGame.Player.PlayerAnimator.Parameters;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Configures the Blender-generated fox FBX and builds its Animator Controller (plan.md §6).
    /// </summary>
    public static class CharacterAssetBuilder
    {
        public const string FoxModelPath = "Assets/Art/Character/Fox.fbx";
        public const string AnimatorControllerPath = "Assets/Art/Character/Fox.controller";

        private const float MoveThreshold = 0.1f;
        private const float FallThreshold = -1f;
        private const float BlendDuration = 0.1f;

        private static readonly string[] LocomotionStates = { "Idle", "Walk", "Run" };
        private static readonly string[] StateNames = { "Idle", "Walk", "Run", "Jump", "Fall" };
        private static readonly string[] LoopingClips = { "Idle", "Walk", "Run", "Fall" };

        private static readonly Dictionary<string, Vector3> StatePositions = new()
        {
            ["Idle"] = new Vector3(300f, 120f),
            ["Walk"] = new Vector3(580f, 40f),
            ["Run"] = new Vector3(580f, 200f),
            ["Jump"] = new Vector3(300f, 320f),
            ["Fall"] = new Vector3(580f, 320f),
        };

        /// <summary>
        /// Paw contacts per clip as (normalized time, legs) for <c>FootprintEmitter.Footstep</c>.
        /// Walk plants its diagonal pairs half a cycle apart; Run (bound) lands the front pair,
        /// then pushes off with the back pair.
        /// </summary>
        private static readonly Dictionary<string, (float Time, string Legs)[]> Footsteps = new()
        {
            ["Walk"] = new[] { (0f, "LegFR,LegBL"), (0.5f, "LegFL,LegBR") },
            ["Run"] = new[] { (0.25f, "LegFL,LegFR"), (0.75f, "LegBL,LegBR") },
        };

        private static readonly (string Name, AnimatorControllerParameterType Type)[] ParameterSpecs =
        {
            (Parameters.SpeedName, AnimatorControllerParameterType.Float),
            (Parameters.RunningName, AnimatorControllerParameterType.Bool),
            (Parameters.GroundedName, AnimatorControllerParameterType.Bool),
            (Parameters.VerticalSpeedName, AnimatorControllerParameterType.Float),
            (Parameters.JumpName, AnimatorControllerParameterType.Trigger),
            (Parameters.WalkPlaybackSpeedName, AnimatorControllerParameterType.Float),
            (Parameters.RunPlaybackSpeedName, AnimatorControllerParameterType.Float),
        };

        [MenuItem("Tools/MyGame/Build Character Assets", priority = 21)]
        public static void Run()
        {
            ConfigureModelImporter();
            EnsureAnimatorController();
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterAssetBuilder] Character assets built.");
        }

        /// <summary>
        /// Generic rig and clips renamed from the exporter's <c>Fox|Walk</c> take names to <c>Walk</c>.
        /// Bake Axis Conversion stays off: it fixes the rest pose but not the root curves, so the
        /// animated model would tip over 90°.
        /// </summary>
        private static void ConfigureModelImporter()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FoxModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;

            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
                clip.loopTime = LoopingClips.Contains(clip.name);
                clip.events = Footsteps.TryGetValue(clip.name, out var steps)
                    ? steps.Select(step => new AnimationEvent
                    {
                        time = step.Time,
                        functionName = nameof(MyGame.Player.FootprintEmitter.Footstep),
                        stringParameter = step.Legs,
                    }).ToArray()
                    : System.Array.Empty<AnimationEvent>();
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Rebuilding a controller regenerates every sub-asset ID, so an existing controller is kept
        /// while its states and parameters still match. Delete the asset to force a rebuild.
        /// </summary>
        private static AnimatorController EnsureAnimatorController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            if (existing != null && HasExpectedLayout(existing))
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(AnimatorControllerPath);
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(FoxModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__"))
                .ToDictionary(clip => clip.name);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
            foreach (var (name, type) in ParameterSpecs)
            {
                controller.AddParameter(name, type);
            }

            var machine = controller.layers[0].stateMachine;
            var states = StateNames.ToDictionary(name => name, name => AddState(machine, name, clips));
            machine.defaultState = states["Idle"];
            states["Walk"].speedParameterActive = true;
            states["Walk"].speedParameter = Parameters.WalkPlaybackSpeedName;
            states["Run"].speedParameterActive = true;
            states["Run"].speedParameter = Parameters.RunPlaybackSpeedName;

            foreach (var from in LocomotionStates)
            {
                foreach (var to in LocomotionStates.Where(to => to != from))
                {
                    AddLocomotionConditions(Connect(states[from], states[to]), to);
                }

                var toFall = Connect(states[from], states["Fall"]);
                toFall.AddCondition(AnimatorConditionMode.IfNot, 0f, Parameters.GroundedName);
                toFall.AddCondition(AnimatorConditionMode.Less, FallThreshold, Parameters.VerticalSpeedName);

                var land = Connect(states["Fall"], states[from]);
                land.AddCondition(AnimatorConditionMode.If, 0f, Parameters.GroundedName);
                AddLocomotionConditions(land, from);
            }

            var toJump = machine.AddAnyStateTransition(states["Jump"]);
            Configure(toJump);
            toJump.canTransitionToSelf = false;
            toJump.AddCondition(AnimatorConditionMode.If, 0f, Parameters.JumpName);

            Connect(states["Jump"], states["Fall"])
                .AddCondition(AnimatorConditionMode.Less, 0f, Parameters.VerticalSpeedName);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Idle: not moving. Walk: moving without sprint. Run: moving with sprint.
        /// </summary>
        private static void AddLocomotionConditions(AnimatorStateTransition transition, string target)
        {
            if (target == "Idle")
            {
                transition.AddCondition(AnimatorConditionMode.Less, MoveThreshold, Parameters.SpeedName);
                return;
            }

            transition.AddCondition(AnimatorConditionMode.Greater, MoveThreshold, Parameters.SpeedName);
            var runningMode = target == "Run" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
            transition.AddCondition(runningMode, 0f, Parameters.RunningName);
        }

        private static bool HasExpectedLayout(AnimatorController controller)
        {
            var expected = ParameterSpecs.Select(spec => (spec.Name, spec.Type)).OrderBy(spec => spec.Name);
            var parameters = controller.parameters.Select(p => (p.name, p.type)).OrderBy(spec => spec.name);
            var states = controller.layers[0].stateMachine.states.Select(s => s.state.name);
            return parameters.SequenceEqual(expected)
                   && states.OrderBy(n => n).SequenceEqual(StateNames.OrderBy(n => n));
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var state = machine.AddState(name, StatePositions[name]);
            state.motion = clips[name];
            return state;
        }

        private static AnimatorStateTransition Connect(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            Configure(transition);
            return transition;
        }

        private static void Configure(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = BlendDuration;
        }
    }
}
