using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// The worlds in play order, each with its stages (plan.md §8.2). Edited with
    /// Tools/MyGame/Map Tool; never overwritten by the builders.
    /// </summary>
    [CreateAssetMenu(menuName = "MyGame/World List", fileName = "WorldList")]
    public class WorldList : ScriptableObject
    {
        [SerializeField] private World[] _worlds = Array.Empty<World>();

        public IReadOnlyList<World> Worlds => _worlds;

        public int Count => _worlds.Length;

        public World this[int index] => _worlds[index];

        public void SetWorlds(World[] worlds) => _worlds = worlds;

        public bool TryGetStage(StageRef stage, out MapLayout layout)
        {
            layout = null;
            if (stage.World < 0 || stage.World >= _worlds.Length || stage.Index < 0 || stage.Index >= _worlds[stage.World].Stages.Count)
            {
                return false;
            }

            layout = _worlds[stage.World].Stages[stage.Index];
            return layout != null;
        }

        /// <summary>Every stage in play order, worlds first to last.</summary>
        public IEnumerable<StageRef> AllStages()
        {
            for (var world = 0; world < _worlds.Length; world++)
            {
                for (var index = 0; index < _worlds[world].Stages.Count; index++)
                {
                    yield return new StageRef(world, index);
                }
            }
        }

        /// <summary>The stage after <paramref name="stage"/> in play order, skipping empty worlds.</summary>
        public bool TryGetNext(StageRef stage, out StageRef next)
        {
            var found = false;
            foreach (var candidate in AllStages())
            {
                if (found)
                {
                    next = candidate;
                    return true;
                }

                found = candidate == stage;
            }

            next = default;
            return false;
        }

        /// <summary>The position of <paramref name="layout"/>, if it is one of the stages.</summary>
        public bool TryFind(MapLayout layout, out StageRef stage)
        {
            foreach (var candidate in AllStages())
            {
                if (TryGetStage(candidate, out var found) && found == layout)
                {
                    stage = candidate;
                    return true;
                }
            }

            stage = default;
            return false;
        }
    }
}
