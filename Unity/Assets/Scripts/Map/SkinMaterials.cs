using System;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>The slab and pillar materials of one skin, as <see cref="MapBuilder"/> applies them.</summary>
    [Serializable]
    public struct SkinMaterials
    {
        [SerializeField] private Skin _skin;
        [SerializeField] private Material _top;
        [SerializeField] private Material _body;

        public SkinMaterials(Skin skin, Material top, Material body)
        {
            _skin = skin;
            _top = top;
            _body = body;
        }

        public Skin Skin => _skin;

        public Material Top => _top;

        public Material Body => _body;
    }
}
