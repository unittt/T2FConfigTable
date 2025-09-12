using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace T2F.ConfigTable
{
    internal class BytesFileMerger : AssetPostprocessor
    {
        
        internal static void GenerateManually()
        {
            var config =  MergeConfig.Instance;
            GenerateCombinedFile(config);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var config = MergeConfig.Instance;
            if (config == null || !config.AutoGenerate) return;

            if (importedAssets.Concat(movedAssets).Any(asset => IsTargetFile(asset, config)))
            {
                EditorApplication.delayCall += () => GenerateCombinedFile(config);
            }
        }

        private static void GenerateCombinedFile(MergeConfig config)
        {
            try
            {
                var files = Directory.GetFiles(config.InputFolder, "*.bytes", SearchOption.AllDirectories)
                    .Where(p => !p.EndsWith(".meta"))
                    .OrderBy(p => p)
                    .ToList();

                string newHash = CalculateFilesHash(files);
                bool outputFileExists = File.Exists(config.OutputFile);

                if (newHash == config.LastHash && outputFileExists)
                {
                    Debug.Log("配置文件不变，跳过生成.");
                    return;
                }

                // 构建字典
                var fileDict = new Dictionary<string, byte[]>();
                foreach (var file in files)
                {
                    string key = Path.GetFileNameWithoutExtension(GetRelativePath(file, config.InputFolder));
                    fileDict[key] = File.ReadAllBytes(file);
                }

                // 确保输出目录存在
                string outputDir = Path.GetDirectoryName(config.OutputFile);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 生成合并文件
                byte[] mergedData = BytesFileHandler.PackBytes(fileDict);
                File.WriteAllBytes(config.OutputFile, mergedData);
                config.LastHash = newHash;
                Debug.Log($"Merged {files.Count} files to {config.OutputFile}");
            }
            catch (Exception e)
            {
                Debug.LogError($"合并配置文件失败:{e}");
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

        private static bool IsTargetFile(string assetPath, MergeConfig config)
        {
            return assetPath.StartsWith(config.InputFolder) 
                && assetPath.EndsWith(".bytes")
                && !assetPath.EndsWith(".meta");
        }
    }
}