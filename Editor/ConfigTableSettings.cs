using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace T2F.ConfigTable.EditorExtensions
{
    /// <summary>
    /// 配置表设置
    /// </summary>
    [FilePath("ProjectSettings/T2F/ConfigTableSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class ConfigTableSettings : ScriptableSingleton<ConfigTableSettings>
    {
        /// <summary>
        /// 合并配置列表
        /// </summary>
        [SerializeField]
        public List<MergeInfo> MergeInfos = new();

        /// <summary>
        /// 是否启用自动生成（当 .bytes 文件变化时自动合并）
        /// </summary>
        public bool AutoGenerate = true;

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            Save(true);
        }
    }

    /// <summary>
    /// 单个合并配置项
    /// </summary>
    [Serializable]
    internal class MergeInfo
    {
        /// <summary>
        /// 配置名称/描述
        /// </summary>
        public string Name = "New Config";

        /// <summary>
        /// 输入目录（包含 .bytes 文件的目录）
        /// </summary>
        public string InputFolder;

        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string OutputFile;

        /// <summary>
        /// 上次生成时的哈希值（用于增量检测）
        /// </summary>
        [HideInInspector]
        public string LastHash;

        /// <summary>
        /// 上次更新时间
        /// </summary>
        [HideInInspector]
        public string LastUpdateTime;

        /// <summary>
        /// 上次合并的文件数量
        /// </summary>
        [HideInInspector]
        public int LastFileCount;

        /// <summary>
        /// 获取格式化的更新时间
        /// </summary>
        public string GetFormattedUpdateTime()
        {
            if (string.IsNullOrEmpty(LastUpdateTime))
                return "从未更新";

            if (DateTime.TryParse(LastUpdateTime, out var dt))
                return dt.ToString("yyyy-MM-dd HH:mm:ss");

            return LastUpdateTime;
        }

        /// <summary>
        /// 更新时间戳
        /// </summary>
        public void UpdateTimestamp(int fileCount)
        {
            LastUpdateTime = DateTime.Now.ToString("O");
            LastFileCount = fileCount;
        }
    }
}
