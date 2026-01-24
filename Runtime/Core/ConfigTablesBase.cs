using System;
using System.Collections.Generic;
using Luban;
using UnityEngine;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 配置表容器基类
    /// 使用泛型实现框架与业务解耦，业务层的 Tables 类继承此基类即可
    /// </summary>
    /// <typeparam name="TSelf">继承类自身的类型</typeparam>
    public abstract class ConfigTablesBase<TSelf> where TSelf : ConfigTablesBase<TSelf>, new()
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static TSelf Instance { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => Instance != null;

        /// <summary>
        /// 使用字节数组字典初始化
        /// </summary>
        /// <param name="bytesDic">表名到字节数组的映射</param>
        public static void Init(Dictionary<string, byte[]> bytesDic)
        {
            if (bytesDic == null)
            {
                Debug.LogError("[ConfigTables] bytesDic is null");
                return;
            }

            Instance = new TSelf();
            Instance.OnLoad(tableName =>
            {
                if (!bytesDic.TryGetValue(tableName, out var bytes))
                {
                    Debug.LogError($"[ConfigTables] Table not found: {tableName}");
                    return null;
                }
                return new ByteBuf(bytes);
            });
            Instance.OnResolveRef();
        }

        /// <summary>
        /// 使用合并后的字节数组初始化
        /// </summary>
        /// <param name="mergedBytes">合并后的字节数组</param>
        public static void Init(byte[] mergedBytes)
        {
            if (mergedBytes == null || mergedBytes.Length == 0)
            {
                Debug.LogError("[ConfigTables] mergedBytes is null or empty");
                return;
            }

            var bytesDic = BytesFileHandler.UnpackBytes(mergedBytes);
            Init(bytesDic);
        }

        /// <summary>
        /// 释放实例
        /// </summary>
        public static void Release()
        {
            Instance = null;
        }

        /// <summary>
        /// 加载所有配置表（由子类实现）
        /// </summary>
        /// <param name="loader">字节加载器，传入表名返回 ByteBuf</param>
        protected abstract void OnLoad(Func<string, ByteBuf> loader);

        /// <summary>
        /// 解析引用关系（由子类实现）
        /// </summary>
        protected abstract void OnResolveRef();
    }
}
