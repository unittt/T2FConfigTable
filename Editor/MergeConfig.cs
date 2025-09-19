using T2F.Core;

namespace T2F.ConfigTable
{

    [System.Serializable]
    [ScriptableFilePath("Assets/T2FConfigTable/Editor/Asset/BytesMergeConfig.asset", ScriptableFileLocation.ProjectFolder)]
    internal class MergeConfig : T2FScriptableSingleton<MergeConfig>
    {
        public string InputFolder = "Assets/T2FConfigTable/Res/Gen";
        public string OutputFile = "Assets/Resources/CombinedBytes.bytes";
        public bool AutoGenerate = true;
        public string LastHash;
    }
}

