using System.Collections.Generic;
using System.IO;
using System.Linq;
using T2F.Core.EditorExtensions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace T2F.ConfigTable.EditorExtensions
{
    /// <summary>
    /// 配置表设置 - ProjectSettings 提供程序
    /// </summary>
    internal sealed class ConfigTableSettingsProvider : T2FSettingsProvider<ConfigTableSettings>
    {
        private ReorderableList _reorderableList;
        private SerializedProperty _mergeInfosProperty;
        private SerializedProperty _autoGenerateProperty;

        protected override string Title => "Config Table";
        protected override string Description => "配置表合并工具\n用于将多个 .bytes 文件合并为单个配置文件";

        private static GUIStyle _infoStyle;
        private static GUIStyle InfoStyle => _infoStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = Color.gray }
        };

        private ConfigTableSettingsProvider(string path, SettingsScope scopes)
            : base(path, scopes) { }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new ConfigTableSettingsProvider(
                "Project/T2F/Config Table",
                SettingsScope.Project
            )
            {
                label = "Config Table",
                keywords = new HashSet<string>(new[] { "T2F", "Config", "Table", "Merge", "Bytes" })
            };
            return provider;
        }

        protected override void OnSettingsActivate(string searchContext, VisualElement rootElement)
        {
            _mergeInfosProperty = SerializedObject.FindProperty("MergeInfos");
            _autoGenerateProperty = SerializedObject.FindProperty("AutoGenerate");

            _reorderableList = new ReorderableList(SerializedObject, _mergeInfosProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "合并配置列表", EditorStyles.boldLabel),
                elementHeight = EditorGUIUtility.singleLineHeight * 5 + 14,
                drawElementCallback = DrawElement,
                onAddCallback = OnAddElement,
                onRemoveCallback = OnRemoveElement
            };
        }

        protected override void DrawSettingsGUI()
        {
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
                // 触发重绘
            }

            EditorGUILayout.EndHorizontal();

            ShowStatusHelpBox();
        }

        protected override void ResetToDefault()
        {
            Settings.MergeInfos.Clear();
            Settings.AutoGenerate = true;
        }

        #region ReorderableList 绘制

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
            var mergeInfo = Settings.MergeInfos[index];
            rect.y += 2;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float buttonWidth = 50f;
            float y = rect.y;

            // 第一行：名称 + 单项合并按钮
            var nameRect = new Rect(rect.x, y, rect.width - buttonWidth - 5, lineHeight);
            var mergeButtonRect = new Rect(nameRect.xMax + 5, y, buttonWidth, lineHeight);

            var nameProp = element.FindPropertyRelative("Name");
            EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("名称"));

            if (GUI.Button(mergeButtonRect, "合并", EditorStyles.miniButton))
            {
                BytesFileMerger.GenerateSingle(mergeInfo);
                AssetDatabase.Refresh();
            }

            y += lineHeight + 2;

            // 第二行：输入目录
            var inputRect = new Rect(rect.x, y, rect.width - buttonWidth - 5, lineHeight);
            var inputButtonRect = new Rect(inputRect.xMax + 5, y, buttonWidth, lineHeight);

            var inputFolderProp = element.FindPropertyRelative("InputFolder");
            EditorGUI.PropertyField(inputRect, inputFolderProp, new GUIContent("输入目录"));

            if (GUI.Button(inputButtonRect, "选择", EditorStyles.miniButton))
            {
                SelectFolder(inputFolderProp);
            }

            y += lineHeight + 2;

            // 第三行：输出文件
            var outputRect = new Rect(rect.x, y, rect.width - buttonWidth - 5, lineHeight);
            var outputButtonRect = new Rect(outputRect.xMax + 5, y, buttonWidth, lineHeight);

            var outputFileProp = element.FindPropertyRelative("OutputFile");
            EditorGUI.PropertyField(outputRect, outputFileProp, new GUIContent("输出文件"));

            if (GUI.Button(outputButtonRect, "选择", EditorStyles.miniButton))
            {
                SelectFile(outputFileProp);
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

        private void OnAddElement(ReorderableList list)
        {
            RecordAndSave("Add merge config");
            Settings.MergeInfos.Add(new MergeInfo());
        }

        private void OnRemoveElement(ReorderableList list)
        {
            var mergeInfo = Settings.MergeInfos[list.index];
            if (EditorUtility.DisplayDialog("删除配置", $"确定要删除配置 \"{mergeInfo.Name}\" 吗？", "删除", "取消"))
            {
                RecordAndSave("Remove merge config");
                Settings.MergeInfos.RemoveAt(list.index);
            }
        }

        #endregion

        #region 辅助方法

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

        private void SelectFolder(SerializedProperty inputFolderProp)
        {
            var path = EditorUtility.OpenFolderPanel("选择输入目录",
                GetValidDirectory(inputFolderProp.stringValue), "");

            if (!string.IsNullOrEmpty(path))
            {
                inputFolderProp.stringValue = ConvertToProjectPath(path);
            }
        }

        private void SelectFile(SerializedProperty outputFileProp)
        {
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
            return Settings.MergeInfos.Any(info =>
                Directory.Exists(info.InputFolder) &&
                !string.IsNullOrEmpty(info.OutputFile));
        }

        private bool IsConfigValid(MergeInfo info)
        {
            return Directory.Exists(info.InputFolder) &&
                   !string.IsNullOrEmpty(info.OutputFile);
        }

        private void ExecuteMergeAll()
        {
            try
            {
                SerializedObject.ApplyModifiedProperties();
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
            var validConfigs = Settings.MergeInfos.Count(IsConfigValid);
            var totalConfigs = Settings.MergeInfos.Count;

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

        #endregion
    }
}
