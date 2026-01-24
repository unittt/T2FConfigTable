using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEditorInternal;

namespace T2F.ConfigTable.EditorExtensions
{
    /// <summary>
    /// 配置表编辑器窗口
    /// </summary>
    internal class ConfigTableEditorWindow : EditorWindow
    {
        private ConfigTableSettings _settings;
        private SerializedObject _serializedObject;
        private SerializedProperty _mergeInfosProperty;
        private SerializedProperty _autoGenerateProperty;
        private ReorderableList _reorderableList;
        private Vector2 _scrollPosition;

        private const float SmallButtonWidth = 50f;
        
        private static GUIStyle _headerStyle;
        private static GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11
        };

        private static GUIStyle _infoStyle;
        private static GUIStyle InfoStyle => _infoStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = Color.gray }
        };

        [MenuItem("T2F/Config Table Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigTableEditorWindow>();
            window.titleContent = new GUIContent("配置表管理工具");
            window.minSize = new Vector2(550, 450);
        }

        private void OnEnable()
        {
            _settings = ConfigTableSettings.instance;
            _serializedObject = new SerializedObject(_settings);
            _mergeInfosProperty = _serializedObject.FindProperty("MergeInfos");
            _autoGenerateProperty = _serializedObject.FindProperty("AutoGenerate");

            InitializeReorderableList();
        }

        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(_serializedObject, _mergeInfosProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "合并配置列表", HeaderStyle),
                elementHeight = EditorGUIUtility.singleLineHeight * 5 + 14,
                drawElementCallback = DrawElement,
                onAddCallback = OnAddElement,
                onRemoveCallback = OnRemoveElement
            };
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
            var mergeInfo = _settings.MergeInfos[index];
            rect.y += 2;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float y = rect.y;

            // 第一行：名称 + 单项合并按钮
            var nameRect = new Rect(rect.x, y, rect.width - SmallButtonWidth - 5, lineHeight);
            var mergeButtonRect = new Rect(nameRect.xMax + 5, y, SmallButtonWidth, lineHeight);

            var nameProp = element.FindPropertyRelative("Name");
            EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("名称"));

            if (GUI.Button(mergeButtonRect, "合并", EditorStyles.miniButton))
            {
                BytesFileMerger.GenerateSingle(mergeInfo);
                AssetDatabase.Refresh();
            }

            y += lineHeight + 2;

            // 第二行：输入目录
            var inputRect = new Rect(rect.x, y, rect.width - SmallButtonWidth - 5, lineHeight);
            var inputButtonRect = new Rect(inputRect.xMax + 5, y, SmallButtonWidth, lineHeight);

            var inputFolderProp = element.FindPropertyRelative("InputFolder");
            EditorGUI.PropertyField(inputRect, inputFolderProp, new GUIContent("输入目录"));

            if (GUI.Button(inputButtonRect, "选择", EditorStyles.miniButton))
            {
                SelectFolder(index);
            }

            y += lineHeight + 2;

            // 第三行：输出文件
            var outputRect = new Rect(rect.x, y, rect.width - SmallButtonWidth - 5, lineHeight);
            var outputButtonRect = new Rect(outputRect.xMax + 5, y, SmallButtonWidth, lineHeight);

            var outputFileProp = element.FindPropertyRelative("OutputFile");
            EditorGUI.PropertyField(outputRect, outputFileProp, new GUIContent("输出文件"));

            if (GUI.Button(outputButtonRect, "选择", EditorStyles.miniButton))
            {
                SelectFile(index);
            }

            y += lineHeight + 2;

            // 第四行：状态信息
            var statusRect = new Rect(rect.x, y, rect.width, lineHeight);
            string statusText = GetStatusText(mergeInfo);
            EditorGUI.LabelField(statusRect, "状态", statusText, InfoStyle);

            y += lineHeight + 2;

            // 第五行：更新时间
            var timeRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.LabelField(timeRect, "更新时间", mergeInfo.GetFormattedUpdateTime(), InfoStyle);
        }

        private string GetStatusText(MergeInfo mergeInfo)
        {
            if (string.IsNullOrEmpty(mergeInfo.InputFolder))
                return "未配置输入目录";

            if (!Directory.Exists(mergeInfo.InputFolder))
                return "输入目录不存在";

            int currentFileCount = Directory.GetFiles(mergeInfo.InputFolder, "*.bytes", SearchOption.AllDirectories)
                .Count(p => !p.EndsWith(".meta"));

            if (currentFileCount == 0)
                return "目录中无 .bytes 文件";

            bool outputExists = !string.IsNullOrEmpty(mergeInfo.OutputFile) && File.Exists(mergeInfo.OutputFile);
            string outputStatus = outputExists ? "已生成" : "未生成";

            if (mergeInfo.LastFileCount > 0 && mergeInfo.LastFileCount != currentFileCount)
                return $"{outputStatus} | {currentFileCount} 个文件 (变化: {currentFileCount - mergeInfo.LastFileCount:+#;-#;0})";

            return $"{outputStatus} | {currentFileCount} 个文件";
        }

        private void OnAddElement(ReorderableList list)
        {
            _settings.MergeInfos.Add(new MergeInfo());
            EditorUtility.SetDirty(_settings);
        }

        private void OnRemoveElement(ReorderableList list)
        {
            var mergeInfo = _settings.MergeInfos[list.index];
            if (EditorUtility.DisplayDialog("删除配置",
                $"确定要删除配置 \"{mergeInfo.Name}\" 吗？", "删除", "取消"))
            {
                _settings.MergeInfos.RemoveAt(list.index);
                EditorUtility.SetDirty(_settings);
            }
        }

        private void OnGUI()
        {
            if (_serializedObject == null) return;

            _serializedObject.Update();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // 绘制可排序列表
            _reorderableList.DoLayoutList();

            EditorGUILayout.Space(10);

            // 自动生成选项
            EditorGUILayout.PropertyField(_autoGenerateProperty, new GUIContent("自动生成", "当 .bytes 文件变化时自动合并"));

            EditorGUILayout.Space(15);

            // 操作按钮
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!IsAnyConfigValid()))
            {
                if (GUILayout.Button("合并所有配置", GUILayout.Height(28)))
                {
                    ExecuteMergeAll();
                }
            }

            if (GUILayout.Button("刷新状态", GUILayout.Height(28), GUILayout.Width(80)))
            {
                Repaint();
            }

            EditorGUILayout.EndHorizontal();

            ShowStatusHelpBox();
            EditorGUILayout.EndScrollView();

            _serializedObject.ApplyModifiedProperties();
        }

        private void SelectFolder(int index)
        {
            var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
            var inputFolderProp = element.FindPropertyRelative("InputFolder");

            var path = EditorUtility.OpenFolderPanel("选择输入目录",
                GetValidDirectory(inputFolderProp.stringValue), "");

            if (!string.IsNullOrEmpty(path))
            {
                inputFolderProp.stringValue = ConvertToProjectPath(path);
            }
        }

        private void SelectFile(int index)
        {
            var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
            var outputFileProp = element.FindPropertyRelative("OutputFile");

            // 安全获取目录
            string directory = Application.dataPath;
            string currentValue = outputFileProp.stringValue;
            if (!string.IsNullOrEmpty(currentValue))
            {
                try
                {
                    var dir = Path.GetDirectoryName(currentValue);
                    if (!string.IsNullOrEmpty(dir))
                        directory = dir;
                }
                catch
                {
                    // 忽略无效路径
                }
            }

            var path = EditorUtility.SaveFilePanel("选择输出文件",
                GetValidDirectory(directory), "merged_config", "bytes");

            if (!string.IsNullOrEmpty(path))
            {
                outputFileProp.stringValue = ConvertToProjectPath(path);
            }
        }

        private string ConvertToProjectPath(string absolutePath)
        {
            var normalizedPath = absolutePath.Replace("\\", "/");
            var normalizedDataPath = Application.dataPath.Replace("\\", "/");

            if (normalizedPath.StartsWith(normalizedDataPath))
            {
                return "Assets" + normalizedPath.Substring(normalizedDataPath.Length);
            }
            return normalizedPath;
        }

        private string GetValidDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return Application.dataPath;

            try
            {
                var normalizedPath = path.Replace("\\", "/");
                return Directory.Exists(normalizedPath) ? normalizedPath : Application.dataPath;
            }
            catch
            {
                return Application.dataPath;
            }
        }

        private bool IsAnyConfigValid()
        {
            return _settings.MergeInfos.Any(info =>
                Directory.Exists(info.InputFolder) &&
                !string.IsNullOrEmpty(info.OutputFile));
        }

        private void ExecuteMergeAll()
        {
            try
            {
                _serializedObject.ApplyModifiedProperties();
                BytesFileMerger.GenerateManually();
                EditorUtility.DisplayDialog("合并完成", "所有配置文件已成功合并", "确定");
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[T2FConfigTable] 合并失败：{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", $"合并过程中发生错误：\n{ex.Message}", "关闭");
            }
        }

        private void ShowStatusHelpBox()
        {
            var validConfigs = _settings.MergeInfos.Count(IsConfigValid);
            var totalConfigs = _settings.MergeInfos.Count;

            EditorGUILayout.Space(5);

            if (totalConfigs == 0)
            {
                EditorGUILayout.HelpBox("没有配置项，点击 + 按钮添加配置", MessageType.Info);
            }
            else if (validConfigs == totalConfigs)
            {
                EditorGUILayout.HelpBox($"所有配置项有效 ({validConfigs}/{totalConfigs})", MessageType.Info);
            }
            else if (validConfigs > 0)
            {
                EditorGUILayout.HelpBox($"部分配置项有效 ({validConfigs}/{totalConfigs})", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("没有有效的配置项，请检查输入目录和输出文件", MessageType.Error);
            }
        }

        private bool IsConfigValid(MergeInfo info)
        {
            return Directory.Exists(info.InputFolder) &&
                   !string.IsNullOrEmpty(info.OutputFile);
        }
    }
}
