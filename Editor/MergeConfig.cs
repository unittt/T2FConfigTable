using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace T2F.ConfigTable.EditorExtensions
{
    /// <summary>
    /// 配置表合并设置
    /// </summary>
    [FilePath("ProjectSettings/T2FConfigTableSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class MergeConfig : ScriptableSingleton<MergeConfig>
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
        public void Save()
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
        public string LastHash;
    }
}
