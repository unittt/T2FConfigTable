using System.Collections.Generic;
using UnityEngine;

namespace T2F.ConfigTable.GameModule
{
    public partial class TbLevelBean
    {
        private List<LevelBean> _loopLevels;
        
        private Dictionary<int, List<LevelBean>> _levelsByType;
        /// <summary>
        /// 循环关卡
        /// </summary>
        public List<LevelBean> LoopLevels
        {
            get
            {
                if (_loopLevels != null) return _loopLevels;
                _loopLevels = new List<LevelBean>();
                foreach (var item in DataList)
                {
                    if (item.IsLoop)
                    {
                        _loopLevels.Add(item);
                    }
                }
                return _loopLevels;
            }
        }


        public bool RandomLevel(int difficulty, out int levelId)
        {
            levelId = -1;
            InitLevelsByType();
            if (_levelsByType.TryGetValue(difficulty, out var list))
            {
                var index = Random.Range(0, list.Count);
                levelId = list[index].Id;
                return true;
            }

            return false;
        }

        private void InitLevelsByType()
        {
            if (_levelsByType != null) return;
            _levelsByType = new Dictionary<int, List<LevelBean>>();
            foreach (var item in DataList)
            {
                if (item.IsLoop)
                {
                    if (!_levelsByType.TryGetValue(item.Difficulty, out var list))
                    {
                        list = new List<LevelBean>();
                        _levelsByType.Add(item.Difficulty, list);
                    }
                    list.Add(item);
                }
            }
        }
    }
}