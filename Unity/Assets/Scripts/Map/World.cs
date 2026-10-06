using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>One themed world: a name, an accent color for the menus, and its stages in order.</summary>
    [Serializable]
    public class World
    {
        [SerializeField] private string _name;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private MapLayout[] _stages = Array.Empty<MapLayout>();

        public World(string name, Color color)
        {
            _name = name;
            _color = color;
        }

        public string Name => _name;

        public Color Color => _color;

        public IReadOnlyList<MapLayout> Stages => _stages;

        public void SetStages(MapLayout[] stages) => _stages = stages;
    }
}
