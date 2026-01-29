using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 配置表设置 - ProjectSettings 提供程序
    /// </summary>
    internal sealed class ConfigTableSettingsProvider : SettingsProvider
    {
        private const float ButtonWidth = 50f;
        private const float VerticalSpacing = 2f;
        private const float LineHeightMultiplier = 5f;
        private const string ConfigTableLabel = "Config Table";
        private const string ConfigDescription = "配置表合并工具\n用于将多个 .bytes 文件合并为单个配置文件";
        
        private ReorderableList _reorderableList;
        private SerializedProperty _mergeInfosProperty;
        private SerializedProperty _autoGenerateProperty;
        private SerializedObject _serializedObject;
        private ConfigTableSettings _settings;

        private static GUIStyle _infoStyle;
        private static GUIStyle InfoStyle => _infoStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = Color.gray },
            wordWrap = true
        };

        private ConfigTableSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new ConfigTableSettingsProvider(
                "Project/T2F/Config Table",
                SettingsScope.Project,
                new HashSet<string>(new[] { "T2F", "Config", "Table", "Merge", "Bytes" })
            )
            {
                label = ConfigTableLabel
            };
            return provider;
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            InitializeSettings();
            InitializeReorderableList();
        }

        public override void OnDeactivate()
        {
            _serializedObject?.Dispose();
            _serializedObject = null;
            _reorderableList = null;
        }

        public override void OnGUI(string searchContext)
        {
            if (!ValidateSettings())
            {
                EditorGUILayout.HelpBox("设置文件加载失败", MessageType.Error);
                return;
            }

            _serializedObject.Update();
            DrawUI();
        }

        #region Initialization Methods

        private void InitializeSettings()
        {
            _settings = ConfigTableSettings.instance;
            _serializedObject = new SerializedObject(_settings);
            _mergeInfosProperty = _serializedObject.FindProperty("MergeInfos");
            _autoGenerateProperty = _serializedObject.FindProperty("AutoGenerate");
        }

        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(_serializedObject, _mergeInfosProperty, 
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true)
            {
                drawHeaderCallback = DrawListHeader,
                elementHeight = EditorGUIUtility.singleLineHeight * LineHeightMultiplier + 14,
                drawElementCallback = DrawListElement,
                onAddCallback = OnListElementAdded,
                onRemoveCallback = OnListElementRemoved
            };
        }

        private bool ValidateSettings()
        {
            return _serializedObject != null && _settings != null;
        }

        #endregion

        #region UI Drawing Methods

        private void DrawUI()
        {
            EditorGUILayout.Space(5);
            DrawHeader();
            EditorGUILayout.Space(10);
            DrawSettings();
            ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(ConfigTableLabel, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(ConfigDescription, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawSettings()
        {
            DrawReorderableList();
            EditorGUILayout.Space(10);
            DrawAutoGenerateOption();
            EditorGUILayout.Space(15);
            DrawActionButtons();
            DrawStatusBox();
            EditorGUILayout.Space(15);
            DrawResetButton();
        }

        private void DrawReorderableList()
        {
            _reorderableList.DoLayoutList();
        }

        private void DrawAutoGenerateOption()
        {
            EditorGUILayout.PropertyField(_autoGenerateProperty, 
                new GUIContent("自动生成", "当 .bytes 文件变化时自动合并"));
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!HasValidConfigurations()))
            {
                if (GUILayout.Button("合并所有配置", GUILayout.Height(28)))
                {
                    ExecuteMergeAll();
                }
            }

            if (GUILayout.Button("刷新状态", GUILayout.Height(28), GUILayout.Width(80)))
            {
                RefreshStatus();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusBox()
        {
            EditorGUILayout.Space(5);
            var statusMessage = GetStatusMessage();
            var messageType = GetStatusMessageType();
            EditorGUILayout.HelpBox(statusMessage, messageType);
        }

        private void DrawResetButton()
        {
            if (GUILayout.Button("重置为默认值"))
            {
                ShowResetConfirmationDialog();
            }
        }

        #endregion

        #region ReorderableList Callbacks

        private void DrawListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "合并配置列表", EditorStyles.boldLabel);
        }

        private void DrawListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
            var mergeInfo = _settings.MergeInfos[index];
            
            rect.y += VerticalSpacing;
            float y = rect.y;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float contentWidth = rect.width - ButtonWidth - 5;

            // 第一行：名称 + 合并按钮
            y = DrawNameRow(rect, element, mergeInfo, y, lineHeight, contentWidth);
            
            // 第二行：输入目录
            y = DrawInputFolderRow(rect, element, y, lineHeight, contentWidth);
            
            // 第三行：输出文件
            y = DrawOutputFileRow(rect, element, y, lineHeight, contentWidth);
            
            // 第四行：状态信息
            y = DrawStatusRow(rect, mergeInfo, y, lineHeight);
            
            // 第五行：更新时间
            DrawUpdateTimeRow(rect, mergeInfo, y, lineHeight);
        }

        private float DrawNameRow(Rect rect, SerializedProperty element, MergeInfo mergeInfo,
            float y, float lineHeight, float contentWidth)
        {
            var nameRect = new Rect(rect.x, y, contentWidth, lineHeight);
            var mergeButtonRect = new Rect(nameRect.xMax + 5, y, ButtonWidth, lineHeight);

            var nameProp = element.FindPropertyRelative("Name");
            EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("名称"));

            if (GUI.Button(mergeButtonRect, "合并", EditorStyles.miniButton))
            {
                ExecuteSingleMerge(mergeInfo);
            }

            return y + lineHeight + VerticalSpacing;
        }

        private float DrawInputFolderRow(Rect rect, SerializedProperty element,
            float y, float lineHeight, float contentWidth)
        {
            var inputRect = new Rect(rect.x, y, contentWidth, lineHeight);
            var inputButtonRect = new Rect(inputRect.xMax + 5, y, ButtonWidth, lineHeight);

            var inputFolderProp = element.FindPropertyRelative("InputFolder");
            EditorGUI.PropertyField(inputRect, inputFolderProp, new GUIContent("输入目录"));

            if (GUI.Button(inputButtonRect, "选择", EditorStyles.miniButton))
            {
                SelectFolder(inputFolderProp);
            }

            return y + lineHeight + VerticalSpacing;
        }

        private float DrawOutputFileRow(Rect rect, SerializedProperty element,
            float y, float lineHeight, float contentWidth)
        {
            var outputRect = new Rect(rect.x, y, contentWidth, lineHeight);
            var outputButtonRect = new Rect(outputRect.xMax + 5, y, ButtonWidth, lineHeight);

            var outputFileProp = element.FindPropertyRelative("OutputFile");
            EditorGUI.PropertyField(outputRect, outputFileProp, new GUIContent("输出文件"));

            if (GUI.Button(outputButtonRect, "选择", EditorStyles.miniButton))
            {
                SelectFile(outputFileProp);
            }

            return y + lineHeight + VerticalSpacing;
        }

        private float DrawStatusRow(Rect rect, MergeInfo mergeInfo, float y, float lineHeight)
        {
            var statusRect = new Rect(rect.x, y, rect.width, lineHeight);
            string statusText = GetMergeInfoStatus(mergeInfo);
            EditorGUI.LabelField(statusRect, "状态", statusText, InfoStyle);
            return y + lineHeight + VerticalSpacing;
        }

        private void DrawUpdateTimeRow(Rect rect, MergeInfo mergeInfo, float y, float lineHeight)
        {
            var timeRect = new Rect(rect.x, y, rect.width, lineHeight);
            EditorGUI.LabelField(timeRect, "更新时间", mergeInfo.GetFormattedUpdateTime(), InfoStyle);
        }

        private void OnListElementAdded(ReorderableList list)
        {
            SaveChanges("添加合并配置");
            _settings.MergeInfos.Add(new MergeInfo());
            _serializedObject.Update();
        }

        private void OnListElementRemoved(ReorderableList list)
        {
            var mergeInfo = _settings.MergeInfos[list.index];
            if (EditorUtility.DisplayDialog("删除配置", 
                $"确定要删除配置 \"{mergeInfo.Name}\" 吗？", "删除", "取消"))
            {
                SaveChanges("删除合并配置");
                _settings.MergeInfos.RemoveAt(list.index);
                _serializedObject.Update();
            }
        }

        #endregion

        #region Helper Methods

        private void RefreshStatus()
        {
            _serializedObject.Update();
            GUI.FocusControl(null);
        }

        private void ExecuteSingleMerge(MergeInfo mergeInfo)
        {
            BytesFileMergerProcessor.GenerateSingle(mergeInfo);
            AssetDatabase.Refresh();
        }

        private void ExecuteMergeAll()
        {
            try
            {
                ApplyModifiedProperties();
                BytesFileMergerProcessor.GenerateManually();
                EditorUtility.DisplayDialog("合并完成", "所有配置文件已成功合并", "确定");
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[T2FConfigTable] 合并失败：{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", $"合并过程中发生错误：\n{ex.Message}", "关闭");
            }
        }

        private void ApplyModifiedProperties()
        {
            if (_serializedObject.ApplyModifiedProperties())
            {
                _settings.SaveConfig();
            }
        }

        private void SaveChanges(string changeDescription)
        {
            Undo.RecordObject(_settings, changeDescription);
            EditorUtility.SetDirty(_settings);
            _settings.SaveConfig();
        }

        private string GetMergeInfoStatus(MergeInfo mergeInfo)
        {
            if (string.IsNullOrEmpty(mergeInfo.InputFolder))
                return "未配置输入目录";

            if (!Directory.Exists(mergeInfo.InputFolder))
                return "输入目录不存在";

            var bytesFiles = Directory.GetFiles(mergeInfo.InputFolder, "*.bytes", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".meta"));
            int currentFileCount = bytesFiles.Count();

            if (currentFileCount == 0)
                return "目录中无 .bytes 文件";

            bool outputExists = !string.IsNullOrEmpty(mergeInfo.OutputFile) && File.Exists(mergeInfo.OutputFile);
            string outputStatus = outputExists ? "已生成" : "未生成";

            if (mergeInfo.LastFileCount > 0 && mergeInfo.LastFileCount != currentFileCount)
            {
                int change = currentFileCount - mergeInfo.LastFileCount;
                string changeSymbol = change > 0 ? "+" : "";
                return $"{outputStatus} | {currentFileCount} 个文件 (变化: {changeSymbol}{change})";
            }

            return $"{outputStatus} | {currentFileCount} 个文件";
        }

        private string GetStatusMessage()
        {
            var totalConfigs = _settings.MergeInfos.Count;
            if (totalConfigs == 0)
                return "没有配置项，点击 + 按钮添加配置";

            var validConfigs = _settings.MergeInfos.Count(IsConfigurationValid);
            
            if (validConfigs == totalConfigs)
                return $"所有配置项有效 ({validConfigs}/{totalConfigs})";
            
            if (validConfigs > 0)
                return $"部分配置项有效 ({validConfigs}/{totalConfigs})";
            
            return "没有有效的配置项，请检查输入目录和输出文件";
        }

        private MessageType GetStatusMessageType()
        {
            var totalConfigs = _settings.MergeInfos.Count;
            if (totalConfigs == 0)
                return MessageType.Info;

            var validConfigs = _settings.MergeInfos.Count(IsConfigurationValid);
            
            if (validConfigs == totalConfigs)
                return MessageType.Info;
            
            if (validConfigs > 0)
                return MessageType.Warning;
            
            return MessageType.Error;
        }

        private bool HasValidConfigurations()
        {
            return _settings.MergeInfos.Any(IsConfigurationValid);
        }

        private bool IsConfigurationValid(MergeInfo info)
        {
            return Directory.Exists(info.InputFolder) && 
                   !string.IsNullOrEmpty(info.OutputFile);
        }

        private void SelectFolder(SerializedProperty inputFolderProp)
        {
            string defaultPath = GetValidDirectory(inputFolderProp.stringValue);
            string path = EditorUtility.OpenFolderPanel("选择输入目录", defaultPath, "");
            
            if (!string.IsNullOrEmpty(path))
            {
                inputFolderProp.stringValue = ConvertToProjectPath(path);
            }
        }

        private void SelectFile(SerializedProperty outputFileProp)
        {
            string defaultPath = GetDirectoryFromPath(outputFileProp.stringValue);
            string path = EditorUtility.SaveFilePanel("选择输出文件", defaultPath, "merged_config", "bytes");

            if (!string.IsNullOrEmpty(path))
            {
                outputFileProp.stringValue = ConvertToProjectPath(path);
            }
        }

        private string GetDirectoryFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return Application.dataPath;

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                return GetValidDirectory(directory);
            }
            catch
            {
                return Application.dataPath;
            }
        }

        private string ConvertToProjectPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return absolutePath;

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
            if (string.IsNullOrEmpty(path))
                return Application.dataPath;

            try
            {
                return Directory.Exists(path) ? path : Application.dataPath;
            }
            catch
            {
                return Application.dataPath;
            }
        }

        private void ShowResetConfirmationDialog()
        {
            if (EditorUtility.DisplayDialog("重置设置", "确定要重置所有设置为默认值吗？", "重置", "取消"))
            {
                ResetToDefault();
            }
        }

        private void ResetToDefault()
        {
            SaveChanges("重置为默认值");
            _settings.MergeInfos.Clear();
            _settings.AutoGenerate = true;
            _serializedObject.Update();
        }

        #endregion
    }
}