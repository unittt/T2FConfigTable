using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 配置表文件合并器
    /// 监听 .bytes 文件变化并自动合并
    /// </summary>
    internal class BytesFileMergerProcessor : AssetPostprocessor
    {
        private static bool _isGenerating;

        /// <summary>
        /// 手动触发所有配置的合并
        /// </summary>
        internal static void GenerateManually()
        {
            var config = ConfigTableSettings.instance;
            if (config == null) return;

            foreach (var mergeInfo in config.MergeInfos)
            {
                GenerateCombinedFile(mergeInfo);
            }

            config.SaveConfig();
        }

        /// <summary>
        /// 手动触发单个配置的合并
        /// </summary>
        internal static void GenerateSingle(MergeInfo mergeInfo)
        {
            if (mergeInfo == null) return;

            GenerateCombinedFile(mergeInfo);
            ConfigTableSettings.instance.SaveConfig();
        }

        /// <summary>
        /// 资源导入后处理
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var config = ConfigTableSettings.instance;
            if (config == null || !config.AutoGenerate) return;

            // 防止重复注册
            if (_isGenerating) return;

            // 合并所有变化的资源路径
            var changedAssets = importedAssets
                .Concat(movedAssets)
                .Concat(deletedAssets)
                .ToArray();

            // 检查是否有目标文件变化
            bool needsGeneration = false;
            foreach (var mergeInfo in config.MergeInfos)
            {
                if (changedAssets.Any(asset => IsTargetFile(asset, mergeInfo)))
                {
                    needsGeneration = true;
                    break;
                }
            }

            if (needsGeneration)
            {
                _isGenerating = true;
                EditorApplication.delayCall += () =>
                {
                    _isGenerating = false;
                    GenerateManually();
                };
            }
        }

        /// <summary>
        /// 生成合并文件
        /// </summary>
        private static void GenerateCombinedFile(MergeInfo mergeInfo)
        {
            try
            {
                // 检查输入目录是否存在
                if (!Directory.Exists(mergeInfo.InputFolder))
                {
                    Debug.LogWarning($"[T2FConfigTable] 输入目录不存在: {mergeInfo.InputFolder}，跳过合并");
                    return;
                }

                // 获取所有 .bytes 文件（已排序）
                var files = Directory.GetFiles(mergeInfo.InputFolder, "*.bytes", SearchOption.AllDirectories)
                    .Where(p => !p.EndsWith(".meta"))
                    .OrderBy(p => p)
                    .ToList();

                if (files.Count == 0)
                {
                    Debug.LogWarning($"[T2FConfigTable] 在目录 {mergeInfo.InputFolder} 中没有找到 .bytes 文件，跳过合并");
                    return;
                }

                // 一次性读取所有文件并计算哈希
                var (fileDict, newHash) = ReadFilesAndCalculateHash(files, mergeInfo.InputFolder);
                bool outputFileExists = File.Exists(mergeInfo.OutputFile);

                // 检查是否需要更新
                if (newHash == mergeInfo.LastHash && outputFileExists)
                {
                    return;
                }

                // 确保输出目录存在
                string outputDir = Path.GetDirectoryName(mergeInfo.OutputFile);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 生成合并文件
                byte[] mergedData = BytesFileHandler.PackBytes(fileDict);
                File.WriteAllBytes(mergeInfo.OutputFile, mergedData);

                // 更新配置信息
                mergeInfo.LastHash = newHash;
                mergeInfo.UpdateTimestamp(files.Count);

                Debug.Log($"[T2FConfigTable] 合并完成: {mergeInfo.Name} ({files.Count} 个文件 -> {mergeInfo.OutputFile})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[T2FConfigTable] 合并失败 [{mergeInfo.Name}]: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 一次性读取所有文件并计算哈希（避免重复读取）
        /// </summary>
        private static (Dictionary<string, byte[]> fileDict, string hash) ReadFilesAndCalculateHash(
            List<string> files, string rootFolder)
        {
            var fileDict = new Dictionary<string, byte[]>();

            using (var md5 = MD5.Create())
            {
                foreach (var file in files)
                {
                    byte[] content = File.ReadAllBytes(file);
                    string key = Path.GetFileNameWithoutExtension(
                        NormalizePath(Path.GetRelativePath(rootFolder, file)));

                    fileDict[key] = content;
                    md5.TransformBlock(content, 0, content.Length, null, 0);
                }

                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string hash = BitConverter.ToString(md5.Hash).Replace("-", "");

                return (fileDict, hash);
            }
        }

        /// <summary>
        /// 规范化路径（统一使用 /）
        /// </summary>
        private static string NormalizePath(string path)
        {
            return path.Replace("\\", "/");
        }

        /// <summary>
        /// 检查是否是目标文件
        /// </summary>
        private static bool IsTargetFile(string assetPath, MergeInfo mergeInfo)
        {
            if (string.IsNullOrEmpty(mergeInfo.InputFolder)) return false;

            var normalizedAssetPath = NormalizePath(assetPath);
            var normalizedInputFolder = NormalizePath(mergeInfo.InputFolder);

            return normalizedAssetPath.StartsWith(normalizedInputFolder)
                && normalizedAssetPath.EndsWith(".bytes");
        }
    }
}
