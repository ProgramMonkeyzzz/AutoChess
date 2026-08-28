using UnityEngine;

namespace Chess
{
    public class ChessRuntimeData
    {
        public int Hp;
        public float CurrentAtkMultipler;
        public int CurrentAtkIncrement;
        public float CurrentRangeMultipler;
        public int CurrentRangeIncrement;
        public Vector2 CurrentPosition;

        public int GetCurrentAtk(int baseAtk)
        {
            return (int)(((float)baseAtk) * CurrentAtkMultipler + ((float)CurrentAtkIncrement));
        }

        public int GetCurrentRange(int baseRange)
        {
            return (int)(((float)baseRange) * CurrentRangeMultipler + ((float)CurrentRangeIncrement));
        }
    }
}