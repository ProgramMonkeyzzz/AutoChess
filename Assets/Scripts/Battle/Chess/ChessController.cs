using Camp;
using Config;
using Unity.Collections;
using UnityEngine;

namespace Chess
{
    public class ChessController
    {
        private ChessTargetSelector _targetSelector;
        private int _chessId;
        private int _camp;
        private ChessBaseData _chessBaseData;
        private ChessRuntimeData _chessRuntimeData;
        private CampGroup _campGroup;
        private FSM.FSM _fsm;

        public int Camp => _camp;
        public ChessRuntimeData RuntimeData => _chessRuntimeData;
        public ChessBaseData BaseData => _chessBaseData;
        public CampGroup CampGroup => _campGroup;
    }
}
