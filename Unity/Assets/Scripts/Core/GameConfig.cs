using UnityEngine;

namespace MyGame.Core
{
    [CreateAssetMenu(menuName = "MyGame/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _itemCount = 4;

        public int ItemCount => _itemCount;
    }
}
