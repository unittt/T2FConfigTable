using System;
using System.Collections.Generic;
using Luban;
using UnityEngine;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 配置表容器基类
    /// 使用泛型实现框架与业务解耦，业务层的 Tables 类继承此基类即可
    /// 支持立即加载和延迟加载两种模式
    /// </summary>
    /// <typeparam name="TSelf">继承类自身的类型</typeparam>
    public abstract class ConfigTablesBase<TSelf> where TSelf : ConfigTablesBase<TSelf>, new()
    {
        #region Fields

        /// <summary>
        /// 原始字节数据（延迟加载模式使用）
        /// </summary>
        private Dictionary<string, byte[]> _bytesDic;

        /// <summary>
        /// 已加载的表名集合
        /// </summary>
        private readonly HashSet<string> _loadedTables = new();

        /// <summary>
        /// 原始字节数据是否已释放
        /// </summary>
        private bool _rawBytesReleased;

        /// <summary>
        /// 总表数量（初始化时记录）
        /// </summary>
        private int _totalTableCount;

        #endregion

        #region Properties

        /// <summary>
        /// 单例实例
        /// </summary>
        public static TSelf Instance { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => Instance != null;

        /// <summary>
        /// 是否为延迟加载模式
        /// </summary>
        public bool IsLazyMode { get; private set; }

        /// <summary>
        /// 是否已解析引用
        /// </summary>
        public bool IsRefResolved { get; private set; }

        /// <summary>
        /// 是否可以延迟加载新表
        /// </summary>
        public bool CanLazyLoad => IsLazyMode && !_rawBytesReleased && _bytesDic != null;

        /// <summary>
        /// 获取已加载的表数量
        /// </summary>
        public int LoadedTableCount => _loadedTables.Count;

        /// <summary>
        /// 获取总表数量
        /// </summary>
        public int TotalTableCount => _totalTableCount;

        /// <summary>
        /// 获取待加载的表数量
        /// </summary>
        public int PendingTableCount => _bytesDic?.Count ?? 0;

        #endregion

        #region Initialization

        /// <summary>
        /// 使用合并后的字节数组初始化（立即加载所有表）
        /// </summary>
        /// <param name="mergedBytes">合并后的字节数组</param>
        /// <returns>是否初始化成功</returns>
        public static bool Init(byte[] mergedBytes)
        {
            if (!CheckCanInit())
                return false;

            if (mergedBytes == null || mergedBytes.Length == 0)
            {
                Debug.LogError("[ConfigTables] mergedBytes is null or empty");
                return false;
            }

            var bytesDic = BytesFileHandler.UnpackBytes(mergedBytes);
            return InitInternal(bytesDic, lazy: false);
        }

        /// <summary>
        /// 使用字节数组字典初始化（立即加载所有表）
        /// </summary>
        /// <param name="bytesDic">表名到字节数组的映射</param>
        /// <returns>是否初始化成功</returns>
        public static bool Init(Dictionary<string, byte[]> bytesDic)
        {
            if (!CheckCanInit())
                return false;

            return InitInternal(bytesDic, lazy: false);
        }

        /// <summary>
        /// 延迟加载模式初始化（仅存储字节数据，按需加载表）
        /// </summary>
        /// <param name="mergedBytes">合并后的字节数组</param>
        /// <returns>是否初始化成功</returns>
        public static bool InitLazy(byte[] mergedBytes)
        {
            if (!CheckCanInit())
                return false;

            if (mergedBytes == null || mergedBytes.Length == 0)
            {
                Debug.LogError("[ConfigTables] mergedBytes is null or empty");
                return false;
            }

            var bytesDic = BytesFileHandler.UnpackBytes(mergedBytes);
            return InitInternal(bytesDic, lazy: true);
        }

        /// <summary>
        /// 延迟加载模式初始化（仅存储字节数据，按需加载表）
        /// </summary>
        /// <param name="bytesDic">表名到字节数组的映射</param>
        /// <returns>是否初始化成功</returns>
        public static bool InitLazy(Dictionary<string, byte[]> bytesDic)
        {
            if (!CheckCanInit())
                return false;

            return InitInternal(bytesDic, lazy: true);
        }

        /// <summary>
        /// 检查是否可以初始化
        /// </summary>
        private static bool CheckCanInit()
        {
            if (Instance == null) return true;
            Debug.LogWarning("[ConfigTables] Already initialized, call Release() first if you want to reinitialize");
            return false;
        }

        /// <summary>
        /// 内部初始化实现
        /// </summary>
        private static bool InitInternal(Dictionary<string, byte[]> bytesDic, bool lazy)
        {
            if (bytesDic == null)
            {
                Debug.LogError("[ConfigTables] bytesDic is null");
                return false;
            }

            Instance = new TSelf
            {
                _bytesDic = bytesDic,
                IsLazyMode = lazy,
                IsRefResolved = false,
                _rawBytesReleased = false,
                _totalTableCount = bytesDic.Count
            };

            if (!lazy)
            {
                // 立即加载所有表
                Instance.OnLoad(tableName =>
                {
                    if (!bytesDic.TryGetValue(tableName, out var bytes))
                    {
                        Debug.LogError($"[ConfigTables] Table not found: {tableName}");
                        return null;
                    }
                    Instance._loadedTables.Add(tableName);
                    return new ByteBuf(bytes);
                });

                // 立即解析引用
                Instance.OnResolveRef();
                Instance.IsRefResolved = true;

                // 立即模式下释放字节数据
                Instance.ReleaseRawBytes();
            }

            return true;
        }

        #endregion

        #region Loading

        /// <summary>
        /// 延迟加载单个表
        /// </summary>
        /// <typeparam name="T">表类型</typeparam>
        /// <param name="field">表字段引用</param>
        /// <param name="tableName">表名（对应数据文件名，不含扩展名）</param>
        /// <param name="factory">表构造函数</param>
        /// <returns>加载后的表实例</returns>
        protected T LoadTableLazy<T>(ref T field, string tableName, Func<ByteBuf, T> factory) where T : class
        {
            if (field != null)
                return field;

            if (!CanLazyLoad)
            {
                Debug.LogError($"[ConfigTables] Cannot lazy load: {tableName}");
                return null;
            }

            var byteBuf = TryTakeTableBytes(tableName);
            if (byteBuf == null)
                return null;

            field = factory(byteBuf);
            return field;
        }

        /// <summary>
        /// 加载所有未加载的表（延迟模式下使用）
        /// </summary>
        /// <returns>是否成功加载</returns>
        private bool LoadAllTables()
        {
            if (!CanLazyLoad)
            {
                Debug.LogWarning("[ConfigTables] Cannot load tables");
                return false;
            }

            OnLoad(TryTakeTableBytes);

            // 所有表加载完成后释放字节字典
            if (_bytesDic is { Count: 0 })
            {
                ReleaseRawBytes();
            }

            return true;
        }

        /// <summary>
        /// 解析所有引用关系（延迟模式下需要手动调用）
        /// 注意：调用前应确保所有相关表已加载
        /// </summary>
        /// <returns>是否成功解析</returns>
        public bool ResolveAllRefs()
        {
            if (IsRefResolved)
            {
                Debug.LogWarning("[ConfigTables] References already resolved");
                return true;
            }

            // 延迟模式下先加载所有表
            if (IsLazyMode && !LoadAllTables())
            {
                return false;
            }

            OnResolveRef();
            IsRefResolved = true;
            return true;
        }

        /// <summary>
        /// 检查表是否已加载
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>是否已加载</returns>
        public bool IsTableLoaded(string tableName)
        {
            return _loadedTables.Contains(tableName);
        }

        /// <summary>
        /// 尝试获取表的字节数据（获取后从字典中移除）
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>字节缓冲区，失败返回 null</returns>
        private ByteBuf TryTakeTableBytes(string tableName)
        {
            if (_loadedTables.Contains(tableName))
                return null;

            if (_bytesDic == null || !_bytesDic.Remove(tableName, out var bytes))
            {
                Debug.LogError($"[ConfigTables] Table not found: {tableName}");
                return null;
            }

            _loadedTables.Add(tableName);
            return new ByteBuf(bytes);
        }

        #endregion

        #region Release

        /// <summary>
        /// 释放实例
        /// </summary>
        public static void Release()
        {
            Instance?.OnRelease();
            Instance = null;
        }

        /// <summary>
        /// 释放原始字节数据（节省内存，但之后无法再延迟加载新表）
        /// 建议在所有需要的表都加载完成后调用
        /// </summary>
        private void ReleaseRawBytes()
        {
            if (_rawBytesReleased)
                return;

            _bytesDic?.Clear();
            _bytesDic = null;
            _rawBytesReleased = true;
        }

        /// <summary>
        /// 释放时的清理逻辑（子类可重写以添加自定义清理）
        /// </summary>
        protected virtual void OnRelease()
        {
            ReleaseRawBytes();
            _loadedTables.Clear();
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// 加载所有配置表（由子类实现）
        /// loader 返回 null 时表示该表已加载或不存在，子类应跳过该表的赋值
        /// </summary>
        /// <param name="loader">字节加载器，传入表名返回 ByteBuf，返回 null 表示跳过</param>
        protected abstract void OnLoad(Func<string, ByteBuf> loader);

        /// <summary>
        /// 解析引用关系（由子类实现）
        /// </summary>
        protected abstract void OnResolveRef();

        #endregion
    }
}
