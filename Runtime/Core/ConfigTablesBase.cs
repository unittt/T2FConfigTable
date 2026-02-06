using System;
using System.Collections.Generic;
using Luban;
using UnityEngine;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 配置表容器基类
    /// </summary>
    public abstract class ConfigTablesBase<TSelf> where TSelf : ConfigTablesBase<TSelf>, new()
    {
        /// <summary>
        /// 加载模式
        /// </summary>
        private enum LoadMode
        {
            None = 0,
            Immediate = 1,
            Lazy = 2,
            Manual = 3
        }

        #region Fields

        private byte[] _rawBytes;                            // 原始数据（Lazy 模式）
        private Dictionary<string, TableIndex> _tableIndex;  // 索引（Lazy 模式）
        private Dictionary<string, byte[]> _pendingBytes;    // 解包数据（Immediate/Manual 模式）
        private HashSet<string> _loadedTables;
        private LoadMode _loadMode = LoadMode.None;

        #endregion

        #region Properties

        /// <summary>
        /// 单例实例
        /// </summary>
        public static TSelf Instance { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _loadMode != LoadMode.None;

        /// <summary>
        /// 引用是否已解析
        /// </summary>
        public bool IsRefResolved { get; private set; }

        /// <summary>
        /// 已加载的表数量
        /// </summary>
        public int LoadedTableCount => _loadedTables?.Count ?? 0;

        /// <summary>
        /// 待加载的表数量
        /// </summary>
        public int PendingTableCount => _tableIndex?.Count ?? _pendingBytes?.Count ?? 0;

        #endregion

        #region Initialization

        /// <summary>
        /// 立即加载所有表
        /// </summary>
        /// <param name="mergedBytes">合并的字节数据</param>
        /// <param name="resolveRefs">是否解析引用（无跨表引用时可设为 false）</param>
        public static bool InitImmediate(byte[] mergedBytes, bool resolveRefs = true)
        {
            if (mergedBytes == null || mergedBytes.Length == 0)
            {
                Debug.LogError("[ConfigTables] mergedBytes is null or empty");
                return false;
            }
            return InitImmediate(BytesFileHandler.UnpackBytes(mergedBytes), resolveRefs);
        }

        /// <summary>
        /// 立即加载所有表
        /// </summary>
        /// <param name="bytesDic">表名到字节的字典</param>
        /// <param name="resolveRefs">是否解析引用（无跨表引用时可设为 false）</param>
        public static bool InitImmediate(Dictionary<string, byte[]> bytesDic, bool resolveRefs = true)
        {
            if (!CreateInstance(LoadMode.Immediate))
                return false;

            Instance._pendingBytes = bytesDic;
            Instance.OnLoad(Instance.ConsumeTableBytes);

            if (resolveRefs)
            {
                Instance.ResolveAllRefs();
            }
            return true;
        }

        /// <summary>
        /// 延迟加载模式 - 访问属性时按需加载（内存友好）
        /// </summary>
        /// <remarks>
        /// 只解析索引，不解包数据，访问时按需提取
        /// </remarks>
        public static bool InitLazy(byte[] mergedBytes)
        {
            if (mergedBytes == null || mergedBytes.Length == 0)
            {
                Debug.LogError("[ConfigTables] mergedBytes is null or empty");
                return false;
            }

            if (!CreateInstance(LoadMode.Lazy))
                return false;

            Instance._rawBytes = mergedBytes;
            Instance._tableIndex = BytesFileHandler.ParseIndex(mergedBytes);
            return true;
        }

        /// <summary>
        /// 延迟加载模式 - 从字典初始化
        /// </summary>
        public static bool InitLazy(Dictionary<string, byte[]> bytesDic)
        {
            if (bytesDic == null || bytesDic.Count == 0)
            {
                Debug.LogError("[ConfigTables] bytesDic is null or empty");
                return false;
            }

            if (!CreateInstance(LoadMode.Lazy))
                return false;

            Instance._pendingBytes = bytesDic;
            return true;
        }

        /// <summary>
        /// 手动加载模式 - 配合 AddTableBytes 使用
        /// </summary>
        public static bool InitManual()
        {
            if (!CreateInstance(LoadMode.Manual))
                return false;

            Instance._pendingBytes = new Dictionary<string, byte[]>();
            return true;
        }

        private static bool CreateInstance(LoadMode mode)
        {
            if (Instance != null)
            {
                Debug.LogWarning("[ConfigTables] Already initialized, call Release() first");
                return false;
            }

            Instance = new TSelf
            {
                _loadMode = mode,
                _loadedTables = new HashSet<string>()
            };
            return true;
        }

        #endregion

        #region Table Loading

        private ByteBuf ConsumeTableBytes(string tableName)
        {
            // Lazy 模式（v3 索引）：零拷贝切片
            if (_tableIndex != null && _tableIndex.Remove(tableName, out var index))
            {
                _loadedTables.Add(tableName);
                return BytesFileHandler.SliceTable(_rawBytes, index);
            }

            // Immediate/Manual 模式或 Lazy(Dictionary) 模式
            if (_pendingBytes != null && _pendingBytes.Remove(tableName, out var data))
            {
                _loadedTables.Add(tableName);
                return new ByteBuf(data);
            }

            return null;
        }

        /// <summary>
        /// 延迟加载单个表（由生成代码的属性访问器调用）
        /// </summary>
        protected T LoadTableLazy<T>(ref T field, string tableName, Func<ByteBuf, T> factory) where T : class
        {
            if (field != null)
                return field;

            var byteBuf = ConsumeTableBytes(tableName);
            if (byteBuf == null)
            {
                Debug.LogError($"[ConfigTables] Table not found: {tableName}");
                return null;
            }

            field = factory(byteBuf);
            return field;
        }

        /// <summary>
        /// 添加表字节数据（仅 Manual 模式可用）
        /// </summary>
        /// <remarks>
        /// 此方法只添加字节数据到待加载队列，实际加载发生在访问对应表属性时
        /// </remarks>
        public bool AddTableBytes(string tableName, byte[] bytes)
        {
            if (_loadMode != LoadMode.Manual)
            {
                Debug.LogError("[ConfigTables] AddTableBytes only available in Manual mode");
                return false;
            }

            if (string.IsNullOrEmpty(tableName))
            {
                Debug.LogError("[ConfigTables] tableName is null or empty");
                return false;
            }

            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError($"[ConfigTables] bytes is null or empty: {tableName}");
                return false;
            }

            if (_pendingBytes == null)
            {
                Debug.LogError("[ConfigTables] Not initialized");
                return false;
            }

            if (_loadedTables.Contains(tableName))
            {
                Debug.LogWarning($"[ConfigTables] Table already loaded: {tableName}");
                return false;
            }

            if (!_pendingBytes.TryAdd(tableName, bytes))
            {
                Debug.LogWarning($"[ConfigTables] Table bytes already pending: {tableName}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查表是否已加载
        /// </summary>
        public bool IsTableLoaded(string tableName) => _loadedTables?.Contains(tableName) ?? false;

        /// <summary>
        /// 检查表是否待加载
        /// </summary>
        public bool IsTablePending(string tableName)
        {
            if (_tableIndex != null)
                return _tableIndex.ContainsKey(tableName);
            return _pendingBytes?.ContainsKey(tableName) ?? false;
        }

        #endregion

        #region Reference Resolution

        /// <summary>
        /// 解析所有引用关系（Lazy/Manual 模式下，需在所有相关表加载后手动调用）
        /// </summary>
        public void ResolveAllRefs(bool force = false)
        {
            if (!force && IsRefResolved)
            {
                Debug.LogWarning("[ConfigTables] References already resolved");
                return;
            }

            OnResolveRef();
            IsRefResolved = true;
        }

        #endregion

        #region Memory Management

        /// <summary>
        /// 清理待加载的字节缓存（释放内存）
        /// </summary>
        /// <remarks>
        /// Lazy 模式使用零拷贝切片，rawBytes 被所有已加载的 Table 间接引用。
        /// 建议在所有需要的表都加载完成后调用此方法。
        /// 调用后无法再加载未访问的表。
        /// </remarks>
        public void ClearPendingBytes()
        {
            _rawBytes = null;
            _tableIndex?.Clear();
            _tableIndex = null;
            _pendingBytes?.Clear();
            _pendingBytes = null;
        }

        /// <summary>
        /// 释放实例
        /// </summary>
        public static void Release()
        {
            Instance?.OnRelease();
            Instance = null;
        }

        protected virtual void OnRelease()
        {
            ClearPendingBytes();
            _loadedTables?.Clear();
            _loadedTables = null;
            _loadMode = LoadMode.None;
            IsRefResolved = false;
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// 加载所有表（由 Luban 生成的子类实现）
        /// </summary>
        protected abstract void OnLoad(Func<string, ByteBuf> loader);

        /// <summary>
        /// 解析所有引用（由 Luban 生成的子类实现）
        /// </summary>
        protected abstract void OnResolveRef();

        #endregion
    }
}
