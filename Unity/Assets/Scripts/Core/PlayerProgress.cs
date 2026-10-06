using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyGame.Map;
using UnityEngine;

namespace MyGame.Core
{
    /// <summary>
    /// Saved progress (plan.md §8.2): which stages are cleared and their best times, kept by stage
    /// name in <c>progress.json</c> so reordering stages never mixes records up. A world is
    /// unlocked once every stage of the world before it is cleared; the first world always is.
    /// </summary>
    public sealed class PlayerProgress
    {
        private const string FileName = "progress.json";

        private readonly string _path;
        private readonly Dictionary<string, float> _bestTimes;

        private PlayerProgress(string path, Dictionary<string, float> bestTimes)
        {
            _path = path;
            _bestTimes = bestTimes;
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Reads <paramref name="path"/> (default: <see cref="DefaultPath"/>); a missing or broken file is empty progress.</summary>
        public static PlayerProgress Load(string path = null)
        {
            path ??= DefaultPath;
            var bestTimes = new Dictionary<string, float>();
            try
            {
                if (File.Exists(path))
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    foreach (var record in data.Stages)
                    {
                        bestTimes[record.Stage] = record.BestTime;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or ArgumentException)
            {
                Debug.LogWarning($"[Progress] Could not read {path}: {exception.Message}");
            }

            return new PlayerProgress(path, bestTimes);
        }

        public void Save()
        {
            var data = new SaveData(_bestTimes.OrderBy(pair => pair.Key).Select(pair => new StageRecord(pair.Key, pair.Value)).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, JsonUtility.ToJson(data, prettyPrint: true));
        }

        public bool IsCleared(MapLayout stage) => stage != null && _bestTimes.ContainsKey(stage.name);

        public bool TryGetBestTime(MapLayout stage, out float time)
        {
            time = 0f;
            return stage != null && _bestTimes.TryGetValue(stage.name, out time);
        }

        /// <summary>Marks the stage cleared; returns true when <paramref name="time"/> is a new best.</summary>
        public bool RecordClear(MapLayout stage, float time)
        {
            if (_bestTimes.TryGetValue(stage.name, out var best) && best <= time)
            {
                return false;
            }

            _bestTimes[stage.name] = time;
            return true;
        }

        public bool IsWorldUnlocked(WorldList worlds, int world)
        {
            if (world <= 0)
            {
                return true;
            }

            var previous = worlds[world - 1].Stages;
            return previous.All(IsCleared) && IsWorldUnlocked(worlds, world - 1);
        }

        public bool IsUnlocked(WorldList worlds, StageRef stage) => IsWorldUnlocked(worlds, stage.World);

        /// <summary>Where Continue goes: the first uncleared stage of an unlocked world, else the last stage.</summary>
        public StageRef ContinueTarget(WorldList worlds)
        {
            var last = default(StageRef);
            foreach (var stage in worlds.AllStages())
            {
                if (!IsUnlocked(worlds, stage))
                {
                    break;
                }

                last = stage;
                if (worlds.TryGetStage(stage, out var layout) && !IsCleared(layout))
                {
                    return stage;
                }
            }

            return last;
        }

        [Serializable]
        private struct SaveData
        {
            [SerializeField] private StageRecord[] _stages;

            public SaveData(StageRecord[] stages) => _stages = stages;

            public StageRecord[] Stages => _stages ?? Array.Empty<StageRecord>();
        }

        [Serializable]
        private struct StageRecord
        {
            [SerializeField] private string _stage;
            [SerializeField] private float _bestTime;

            public StageRecord(string stage, float bestTime)
            {
                _stage = stage;
                _bestTime = bestTime;
            }

            public string Stage => _stage;

            public float BestTime => _bestTime;
        }
    }
}
