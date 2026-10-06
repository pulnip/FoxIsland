using System;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>One fixed item: the cell it floats over and whether it needs a jump.</summary>
    [Serializable]
    public struct ItemPlacement : IEquatable<ItemPlacement>
    {
        [SerializeField] private Vector2Int _cell;
        [SerializeField] private bool _inAir;

        public ItemPlacement(Vector2Int cell, bool inAir)
        {
            _cell = cell;
            _inAir = inAir;
        }

        public Vector2Int Cell => _cell;

        /// <summary>At jump height instead of ground height.</summary>
        public bool InAir => _inAir;

        public bool Equals(ItemPlacement other) => _cell == other._cell && _inAir == other._inAir;

        public override bool Equals(object obj) => obj is ItemPlacement other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_cell, _inAir);
    }
}
