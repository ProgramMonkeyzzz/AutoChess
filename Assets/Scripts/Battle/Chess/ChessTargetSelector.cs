using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chess
{
    public class ChessTargetSelector
    {
        /// <summary>
        /// 棋子索敌方式，数值与行为树 SelectTarget 的 Mode 参数一致。
        /// </summary>
        public enum TargetSelectMode
        {
            /// <summary>最近的敌方单位。</summary>
            Nearest = 0,

            /// <summary>最远的敌方单位。</summary>
            Farthest = 1,

            /// <summary>当前生命最低的敌方单位。</summary>
            LowestHp = 2,

            /// <summary>当前生命最高的敌方单位。</summary>
            HighestHp = 3,

            /// <summary>运行时射程最大的敌方单位。</summary>
            LongestRange = 4,

            /// <summary>当前攻击力最高的敌方单位。</summary>
            HighestAtk = 5
        }

        public ChessController SelectedChessController { get; private set; }

        private readonly ChessController _owner;

        public ChessTargetSelector(ChessController owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 按指定索敌方式选择当前目标；非法枚举时保持原目标并返回 false。
        /// </summary>
        public bool Select(TargetSelectMode mode)
        {
            switch (mode)
            {
                case TargetSelectMode.Nearest:
                    FindNearestEnemy();
                    return true;
                case TargetSelectMode.Farthest:
                    FindFarthestEnemy();
                    return true;
                case TargetSelectMode.LowestHp:
                    FindLowestHpEnemy();
                    return true;
                case TargetSelectMode.HighestHp:
                    FindHighestHpEnemy();
                    return true;
                case TargetSelectMode.LongestRange:
                    FindLongestRangeEnemy();
                    return true;
                case TargetSelectMode.HighestAtk:
                    FindHighestAtkEnemy();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取所有敌方棋子；阵营数据缺失时返回空序列。
        /// </summary>
        private IEnumerable<ChessController> GetEnemyChesses()
        {
            if (_owner == null || _owner.CampGroup == null || _owner.CampGroup.CampManager == null)
            {
                return Array.Empty<ChessController>();
            }

            return _owner.CampGroup.CampManager.GetEnemyChesses(_owner.Camp);
        }

        /// <summary>
        /// 索敌：敌方当前生命最低的单位
        /// </summary>
        public void FindLowestHpEnemy()
        {
            var enemies = GetEnemyChesses();
            SelectedChessController = null;
            int lowestHp = int.MaxValue;

            foreach (var enemy in enemies)
            {
                if (enemy.RuntimeData.Hp < lowestHp)
                {
                    lowestHp = enemy.RuntimeData.Hp;
                    SelectedChessController = enemy;
                }
            }
        }

        /// <summary>
        /// 索敌：最近的敌方单位
        /// </summary>
        public void FindNearestEnemy()
        {
            var enemies = GetEnemyChesses();
            SelectedChessController = null;
            float minDistance = float.MaxValue;
            Vector2 ownerPos = _owner.RuntimeData.CurrentPosition;

            foreach (var enemy in enemies)
            {
                float distance = Vector2.Distance(ownerPos, enemy.RuntimeData.CurrentPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    SelectedChessController = enemy;
                }
            }
        }

        /// <summary>
        /// 索敌：最远的敌方单位
        /// </summary>
        public void FindFarthestEnemy()
        {
            var enemies = GetEnemyChesses();
            SelectedChessController = null;
            float maxDistance = float.MinValue;
            Vector2 ownerPos = _owner.RuntimeData.CurrentPosition;

            foreach (var enemy in enemies)
            {
                float distance = Vector2.Distance(ownerPos, enemy.RuntimeData.CurrentPosition);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    SelectedChessController = enemy;
                }
            }
        }

        /// <summary>
        /// 索敌：敌方当前生命最高的单位
        /// </summary>
        public void FindHighestHpEnemy()
        {
            var enemies = GetEnemyChesses();
            SelectedChessController = null;
            int highestHp = int.MinValue;

            foreach (var enemy in enemies)
            {
                if (enemy.RuntimeData.Hp > highestHp)
                {
                    highestHp = enemy.RuntimeData.Hp;
                    SelectedChessController = enemy;
                }
            }
        }

        /// <summary>
        /// 索敌：Range最大的敌方单位
        /// </summary>
        public void FindLongestRangeEnemy()
        {
            var enemies = GetEnemyChesses();
            SelectedChessController = null;
            float maxRange = float.MinValue;

            foreach (var enemy in enemies)
            {
                float currentRange = enemy.RuntimeData.GetCurrentRange(enemy.BaseData.Range);
                if (currentRange > maxRange)
                {
                    maxRange = currentRange;
                    SelectedChessController = enemy;
                }
            }
        }

        /// <summary>
        /// 索敌：当前Atk最高的敌方单位
        /// </summary>
        public void FindHighestAtkEnemy()
        {
            var enemies = GetEnemyChesses();
            SelectedChessController = null;
            int highestAtk = int.MinValue;

            foreach (var enemy in enemies)
            {
                int currentAtk = enemy.RuntimeData.GetCurrentAtk(enemy.BaseData.Atk);
                if (currentAtk > highestAtk)
                {
                    highestAtk = currentAtk;
                    SelectedChessController = enemy;
                }
            }
        }
    }
}
