using UnityEngine;

namespace Config
{
    [CreateAssetMenu(menuName = "Chess/ChessBaseData")]
    public class ChessBaseData : ScriptableObject
    {
        public const float Radius = 0.5f;
        
        public int Id;
        public string Name;
        public int Hp;
        public int Atk;
        public int Range;
        public int MoveSpeed;
        public int AtkSpeed;
    }
}
