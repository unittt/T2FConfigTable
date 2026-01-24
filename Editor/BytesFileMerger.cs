using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace T2F.ConfigTable.EditorExtensions
{
    internal class BytesFileMerger : AssetPostprocessor
    {
        internal static void GenerateManually()
        {
            var config = MergeConfig.instance;
            if (config == null) return;
            
            foreach (var mergeInfo in config.MergeInfos)
            {
                GenerateCombinedFile(mergeInfo);
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var config = MergeConfig.instance;
            if (config == null || !config.AutoGenerate) return;

            bool needsGeneration = false;
            
            // 检查每个配置项是否需要重新生成
            foreach (var mergeInfo in config.MergeInfos)
            {
                if (importedAssets.Concat(movedAssets).Any(asset => 
                    IsTargetFile(asset, mergeInfo)))
                {
                    needsGeneration = true;
                    break;
                }
            }

            if (needsGeneration)
            {
                EditorApplication.delayCall += GenerateManually;
            }
        }

        private static void GenerateCombinedFile(MergeInfo mergeInfo)
        {
            try
            {
                // 检查输入目录是否存在
                if (!Directory.Exists(mergeInfo.InputFolder))
                {
                    Debug.LogWarning($"输入目录不存在: {mergeInfo.InputFolder}，跳过合并");
                    return;
                }

                var files = Directory.GetFiles(mergeInfo.InputFolder, "*.bytes", SearchOption.AllDirectories)
                    .Where(p => !p.EndsWith(".meta"))
                    .OrderBy(p => p)
                    .ToList();

                if (files.Count == 0)
                {
                    Debug.LogWarning($"在目录 {mergeInfo.InputFolder} 中没有找到.bytes文件，跳过合并");
                    return;
                }

                string newHash = CalculateFilesHash(files);
                bool outputFileExists = File.Exists(mergeInfo.OutputFile);

                if (newHash == mergeInfo.LastHash && outputFileExists)
                {
                    Debug.Log($"配置文件未变化，跳过生成: {mergeInfo.OutputFile}");
                    return;
                }

                // 构建字典
                var fileDict = new Dictionary<string, byte[]>();
                foreach (var file in files)
                {
                    string key = Path.GetFileNameWithoutExtension(GetRelativePath(file, mergeInfo.InputFolder));
                    fileDict[key] = File.ReadAllBytes(file);
                }

                // 确保输出目录存在
                string outputDir = Path.GetDirectoryName(mergeInfo.OutputFile);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 生成合并文件
                byte[] mergedData = BytesFileHandler.PackBytes(fileDict);
                File.WriteAllBytes(mergeInfo.OutputFile, mergedData);
                mergeInfo.LastHash = newHash;

                // 标记配置已修改
                EditorUtility.SetDirty(MergeConfig.instance);
                Debug.Log($"Merged {files.Count} files to {mergeInfo.OutputFile}");
            }
            catch (Exception e)
            {
                Debug.LogError($"合并配置文件失败: {e.Message}\n{e.StackTrace}");
            }
        }

        private static string CalculateFilesHash(List<string> files)
        {
            using (var md5 = MD5.Create())
            {
                foreach (var file in files.OrderBy(p => p))
                {
                    byte[] content = File.ReadAllBytes(file);
                    md5.TransformBlock(content, 0, content.Length, null, 0);
                }
                md5.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(md5.Hash).Replace("-", "");
            }
        }

        private static string GetRelativePath(string fullPath, string root)
        {
            return Path.GetRelativePath(root, fullPath).Replace("\\", "/");
        }

        private static bool IsTargetFile(string assetPath, MergeInfo mergeInfo)
        {
            return assetPath.StartsWith(mergeInfo.InputFolder) 
                && assetPath.EndsWith(".bytes")
                && !assetPath.EndsWith(".meta");
        }
    }
}