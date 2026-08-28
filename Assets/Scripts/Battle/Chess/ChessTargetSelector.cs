using System.Collections.Generic;
using Camp;
using UnityEngine;

namespace Chess
{
    public class ChessTargetSelector
    {
        public ChessController SelectedChessController { get; private set; }

        private readonly ChessController _owner;

        public ChessTargetSelector(ChessController owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 获取所有敌方棋子
        /// </summary>
        private IEnumerable<ChessController> GetEnemyChesses()
        {
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
            int maxRange = int.MinValue;

            foreach (var enemy in enemies)
            {
                int currentRange = enemy.RuntimeData.GetCurrentRange(enemy.BaseData.Range);
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
