using UnityEditor;
using UnityEngine;
using System.IO;

namespace T2F.ConfigTable
{
    internal class MergeConfigEditor : EditorWindow
    {
        private MergeConfig _mergeConfig;
        private const float _buttonWidth = 60f;
        // 修改为属性延迟初始化
        private static GUIStyle _buttonStyle;
        private static GUIStyle ButtonStyle
        {
            get
            {
                if (_buttonStyle == null)
                {
                    _buttonStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        fontSize = 10,
                        padding = new RectOffset(4, 4, 2, 2)
                    };
                }
                return _buttonStyle;
            }
        }

        [MenuItem("T2F/Config Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<MergeConfigEditor>();
            window.titleContent = new GUIContent("配置合并工具");
            window.minSize = new Vector2(400, 150);
        }

        private void OnEnable()
        {
            _mergeConfig = MergeConfig.Instance;
        }
        

        private void OnGUI()
        {
            if (_mergeConfig == null)
            {
                return;
            }
            
            EditorGUIUtility.labelWidth = 120f;
            DrawPathSelector("输入目录", ref _mergeConfig.InputFolder, SelectFolder);
            DrawPathSelector("输出文件", ref _mergeConfig.OutputFile, SelectFile);

            EditorGUILayout.Space(10);
            
            var autoGenerate = EditorGUILayout.ToggleLeft("自动生成配置", _mergeConfig.AutoGenerate);
            if (autoGenerate != _mergeConfig.AutoGenerate)
            {
                _mergeConfig.AutoGenerate = autoGenerate;
            }
            
            EditorGUILayout.Space(15);

            using (new EditorGUI.DisabledScope(!IsValidConfig()))
            {
                if (GUILayout.Button("立即合并配置", GUILayout.Height(30)))
                {
                    ExecuteMerge();
                }
            }

            ShowStatusHelpBox();
        }

        private void DrawPathSelector(string label, ref string path, System.Action<string> onSelect)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var newPath = EditorGUILayout.TextField(label, path);
                if (newPath!= path)
                {
                    path = newPath;
                }

                if (GUILayout.Button("选择", ButtonStyle, GUILayout.Width(_buttonWidth)))
                {
                    onSelect?.Invoke(path);
                }
            }
        }

        private void SelectFolder(string currentPath)
        {
            var path = EditorUtility.OpenFolderPanel("选择输入目录", 
                GetValidDirectory(currentPath), 
                "");

            if (!string.IsNullOrEmpty(path))
            {
                UpdatePath(ref _mergeConfig.InputFolder, 
                    ConvertToProjectPath(path));
            }
        }

        private void SelectFile(string currentPath)
        {
            var path = EditorUtility.SaveFilePanel("选择输出文件",
                GetValidDirectory(currentPath),
                "merged_config",
                "bytes");

            if (!string.IsNullOrEmpty(path))
            {
                UpdatePath(ref _mergeConfig.OutputFile, 
                    ConvertToProjectPath(path));
            }
        }

        private void UpdatePath(ref string target, string newPath)
        {
            if (target != newPath && IsValidPath(newPath))
            {
                target = newPath;
            }
        }

        private string ConvertToProjectPath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return absolutePath;
        }

        private string GetValidDirectory(string path)
        {
            try
            {
                return Directory.Exists(path) ? path : Application.dataPath;
            }
            catch
            {
                return Application.dataPath;
            }
        }

        private bool IsValidPath(string path)
        {
            return !string.IsNullOrEmpty(path) && 
                   (path.StartsWith("Assets/") || path.StartsWith("ProjectSettings/"));
        }

        private bool IsValidConfig()
        {
            return Directory.Exists(_mergeConfig.InputFolder) && 
                   !string.IsNullOrEmpty(_mergeConfig.OutputFile);
        }

        private void ExecuteMerge()
        {
            try
            {
                BytesFileMerger.GenerateManually();
                EditorUtility.DisplayDialog("合并成功", 
                    $"配置文件已成功合并至：\n{_mergeConfig.OutputFile}", 
                    "确定");
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"合并失败：{ex.Message}");
                EditorUtility.DisplayDialog("错误", 
                    $"合并过程中发生错误：\n{ex.Message}", 
                    "关闭");
            }
        }

        private void ShowStatusHelpBox()
        {
            var status = IsValidConfig() ? 
                "配置参数有效" : 
                "请检查输入目录和输出文件路径是否正确";
            
            var type = IsValidConfig() ? 
                MessageType.Info : 
                MessageType.Error;

            EditorGUILayout.HelpBox(status, type);
        }
    }
}