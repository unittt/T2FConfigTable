using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEditorInternal;

namespace T2F.ConfigTable.EditorExtensions
{
    internal class MergeConfigEditor : EditorWindow
    {
        private MergeConfig _mergeConfig;
        private SerializedObject _serializedObject;
        private SerializedProperty _mergeInfosProperty;
        private SerializedProperty _autoGenerateProperty;
        private ReorderableList _reorderableList;
        private Vector2 _scrollPosition;

        private const float _buttonWidth = 60f;
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
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable()
        {
            _mergeConfig = MergeConfig.Instance;
            _serializedObject = new SerializedObject(_mergeConfig);
            _mergeInfosProperty = _serializedObject.FindProperty("MergeInfos");
            _autoGenerateProperty = _serializedObject.FindProperty("AutoGenerate");
            
            InitializeReorderableList();
        }
        
        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(_serializedObject, _mergeInfosProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "合并配置列表"),
                elementHeight = EditorGUIUtility.singleLineHeight * 3 + 8,
                drawElementCallback = (rect, index, isActive, isFocused) => {
                    var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
                    rect.y += 2;
                    
                    // 输入目录
                    var inputRect = new Rect(rect.x, rect.y, rect.width - _buttonWidth - 5, EditorGUIUtility.singleLineHeight);
                    var outputRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 2, rect.width - _buttonWidth - 5, EditorGUIUtility.singleLineHeight);
                    var hashRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 2 + 4, rect.width, EditorGUIUtility.singleLineHeight);
                    
                    var inputFolderProp = element.FindPropertyRelative("InputFolder");
                    var outputFileProp = element.FindPropertyRelative("OutputFile");
                    var lastHashProp = element.FindPropertyRelative("LastHash");
                    
                    EditorGUI.PropertyField(inputRect, inputFolderProp, new GUIContent("输入目录"));
                    if (GUI.Button(new Rect(inputRect.xMax + 5, rect.y, _buttonWidth, EditorGUIUtility.singleLineHeight), "选择", ButtonStyle))
                    {
                        SelectFolder(index);
                    }
                    
                    // 输出文件
                    EditorGUI.PropertyField(outputRect, outputFileProp, new GUIContent("输出文件"));
                    if (GUI.Button(new Rect(outputRect.xMax + 5, rect.y + EditorGUIUtility.singleLineHeight + 2, _buttonWidth, EditorGUIUtility.singleLineHeight), "选择", ButtonStyle))
                    {
                        SelectFile(index);
                    }
                    
                    // 哈希值（只读）
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.TextField(hashRect, "上次哈希", lastHashProp.stringValue);
                    EditorGUI.EndDisabledGroup();
                },
                onAddCallback = list => {
                    _mergeConfig.MergeInfos.Add(new MergeInfo());
                    EditorUtility.SetDirty(_mergeConfig);
                },
                onRemoveCallback = list => {
                    if (EditorUtility.DisplayDialog("警告", "确定要删除此配置项吗？", "是", "否"))
                    {
                        _mergeConfig.MergeInfos.RemoveAt(list.index);
                        EditorUtility.SetDirty(_mergeConfig);
                    }
                }
            };
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
            EditorGUILayout.PropertyField(_autoGenerateProperty, new GUIContent("自动生成配置"));
            
            EditorGUILayout.Space(15);
            
            // 合并按钮
            using (new EditorGUI.DisabledScope(!IsAnyConfigValid()))
            {
                if (GUILayout.Button("立即合并所有配置", GUILayout.Height(30)))
                {
                    ExecuteMerge();
                }
            }
            
            ShowStatusHelpBox();
            EditorGUILayout.EndScrollView();
            _serializedObject.ApplyModifiedProperties();
        }
        
        private void SelectFolder(int index)
        {
            var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
            var inputFolderProp = element.FindPropertyRelative("InputFolder");
            
            var path = EditorUtility.OpenFolderPanel("选择输入目录", 
                GetValidDirectory(inputFolderProp.stringValue), 
                "");
            
            if (!string.IsNullOrEmpty(path))
            {
                inputFolderProp.stringValue = ConvertToProjectPath(path);
            }
        }
        
        private void SelectFile(int index)
        {
            var element = _mergeInfosProperty.GetArrayElementAtIndex(index);
            var outputFileProp = element.FindPropertyRelative("OutputFile");
            var currentPath = outputFileProp.stringValue;
            var directory = Path.GetDirectoryName(currentPath);
            
            var path = EditorUtility.SaveFilePanel("选择输出文件",
                GetValidDirectory(directory),
                "merged_config",
                "bytes");
            
            if (!string.IsNullOrEmpty(path))
            {
                outputFileProp.stringValue = ConvertToProjectPath(path);
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

        private bool IsAnyConfigValid()
        {
            return _mergeConfig.MergeInfos.Any(info => 
                Directory.Exists(info.InputFolder) && 
                !string.IsNullOrEmpty(info.OutputFile));
        }

        private void ExecuteMerge()
        {
            try
            {
                _serializedObject.ApplyModifiedProperties();
                BytesFileMerger.GenerateManually();
                EditorUtility.DisplayDialog("合并成功", 
                    "所有配置文件已成功合并", 
                    "确定");
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"合并失败：{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", 
                    $"合并过程中发生错误：\n{ex.Message}", 
                    "关闭");
            }
        }

        private void ShowStatusHelpBox()
        {
            var validConfigs = _mergeConfig.MergeInfos.Count(IsConfigValid);
            var totalConfigs = _mergeConfig.MergeInfos.Count;
            
            if (totalConfigs == 0)
            {
                EditorGUILayout.HelpBox("没有配置项，请添加配置", MessageType.Warning);
                return;
            }
            
            if (validConfigs == totalConfigs)
            {
                EditorGUILayout.HelpBox($"所有配置项有效 ({validConfigs}/{totalConfigs})", MessageType.Info);
            }
            else if (validConfigs > 0)
            {
                EditorGUILayout.HelpBox($"部分配置项有效 ({validConfigs}/{totalConfigs})", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("没有有效的配置项", MessageType.Error);
            }
        }
        
        private bool IsConfigValid(MergeInfo info)
        {
            return Directory.Exists(info.InputFolder) && 
                   !string.IsNullOrEmpty(info.OutputFile);
        }
    }
}