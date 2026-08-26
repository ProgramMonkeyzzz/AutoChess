using System;
using UnityEngine;

namespace Config
{
    [Serializable]
    public class ChessBaseData
    {
        public const float Radius = 0.5f;
        
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
