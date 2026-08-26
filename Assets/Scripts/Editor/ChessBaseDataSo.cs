using UnityEngine;

namespace Editor
{
    [CreateAssetMenu(menuName = "Chess/ChessBaseData")]
    public class ChessBaseDataSo : ScriptableObject
    {
        public int ChessId;
        public string Name;
        public int MaxHp;
        public int Atk;
        public int Range;
        public int MoveSpeed;
        public int AtkSpeed;
        public float WindUpTime;
    }
}