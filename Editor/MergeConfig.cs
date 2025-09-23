using System;
using System.Collections.Generic;
using T2F.Core;
using T2F.Core.EditorExtensions;
using UnityEngine;

namespace T2F.ConfigTable.EditorExtensions
{
    [Serializable]
    [ScriptableFilePath("Assets/MergeConfig.asset")]
    internal class MergeConfig : T2FScriptableSingleton<MergeConfig>
    {
        [SerializeField]
        public List<MergeInfo> MergeInfos = new();
        public bool AutoGenerate = true;
    }

    [Serializable]
    internal class MergeInfo
    {
        public string InputFolder;
        public string OutputFile;
        public string LastHash;
    }
}

