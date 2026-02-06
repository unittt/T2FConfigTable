using System;
using System.Collections.Generic;
using Luban;
using UnityEngine;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 内存统计信息
    /// </summary>
    public struct MemoryInfo
    {
        /// <summary>
        /// 原始数据大小（字节）
        /// </summary>
        public long RawBytesSize;

        /// <summary>
        /// 待加载数据大小（字节）
        /// </summary>
        public long PendingBytesSize;

        /// <summary>
        /// 索引数量
        /// </summary>
        public int IndexCount;

        /// <summary>
        /// 已加载表数量
        /// </summary>
        public int LoadedTableCount;
    }

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

        private byte[] _rawBytes;                            // 原始合并数据（零拷贝模式使用）
        private Dictionary<string, TableIndex> _tableIndex;  // 表索引（零拷贝模式使用）
        private Dictionary<string, byte[]> _pendingBytes;    // 待加载的独立表数据
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
        /// 初始化配置表（零拷贝）
        /// </summary>
        /// <param name="mergedBytes">合并的字节数据</param>
        /// <param name="lazy">是否延迟加载（true: 首次访问时加载，false: 立即加载所有表）</param>
        /// <param name="resolveRefs">是否解析引用（仅立即加载模式有效）</param>
        public static bool Init(byte[] mergedBytes, bool lazy = false, bool resolveRefs = true)
        {
            return lazy ? InitLazy(mergedBytes) : InitImmediate(mergedBytes, resolveRefs);
        }

        /// <summary>
        /// 立即加载所有表（零拷贝）
        /// </summary>
        /// <param name="mergedBytes">合并的字节数据</param>
        /// <param name="resolveRefs">是否解析引用（无跨表引用时可设为 false）</param>
        public static bool InitImmediate(byte[] mergedBytes, bool resolveRefs = true)
        {
            if (!ValidateBytes(mergedBytes, "mergedBytes") || !CreateInstance(LoadMode.Immediate))
                return false;

            // 使用零拷贝方式：只解析索引，OnLoad 时切片
            Instance._rawBytes = mergedBytes;
            Instance._tableIndex = BytesFileHandler.ParseIndex(mergedBytes);

            CompleteImmediateInit(resolveRefs);

            // 加载完成后清理索引（rawBytes 保留，被 Table 引用）
            Instance._tableIndex = null;
            return true;
        }

        /// <summary>
        /// 立即加载所有表
        /// </summary>
        /// <param name="bytesDic">表名到字节的字典</param>
        /// <param name="resolveRefs">是否解析引用（无跨表引用时可设为 false）</param>
        public static bool InitImmediate(Dictionary<string, byte[]> bytesDic, bool resolveRefs = true)
        {
            if (!ValidateDictionary(bytesDic, "bytesDic") || !CreateInstance(LoadMode.Immediate))
                return false;

            Instance._pendingBytes = bytesDic;
            CompleteImmediateInit(resolveRefs);
            return true;
        }

        private static void CompleteImmediateInit(bool resolveRefs)
        {
            Instance.OnLoad(Instance.ConsumeTableBytes);
            if (resolveRefs)
            {
                Instance.ResolveAllRefs();
            }
        }

        /// <summary>
        /// 延迟加载模式 - 访问属性时按需加载（内存友好）
        /// </summary>
        /// <remarks>
        /// 只解析索引，不解包数据，访问时按需提取
        /// </remarks>
        public static bool InitLazy(byte[] mergedBytes)
        {
            if (!ValidateBytes(mergedBytes, "mergedBytes") || !CreateInstance(LoadMode.Lazy))
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
            if (!ValidateDictionary(bytesDic, "bytesDic") || !CreateInstance(LoadMode.Lazy))
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

        private static bool ValidateBytes(byte[] bytes, string paramName)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError($"[ConfigTables] {paramName} is null or empty");
                return false;
            }
            return true;
        }

        private static bool ValidateDictionary(Dictionary<string, byte[]> dict, string paramName)
        {
            if (dict == null || dict.Count == 0)
            {
                Debug.LogError($"[ConfigTables] {paramName} is null or empty");
                return false;
            }
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

            if (string.IsNullOrEmpty(tableName) || bytes == null || bytes.Length == 0)
            {
                Debug.LogError($"[ConfigTables] Invalid parameters: tableName={tableName ?? "null"}, bytes={(bytes == null ? "null" : bytes.Length.ToString())}");
                return false;
            }

            if (_pendingBytes == null)
            {
                Debug.LogError("[ConfigTables] Not initialized");
                return false;
            }

            if (_loadedTables.Contains(tableName) || !_pendingBytes.TryAdd(tableName, bytes))
            {
                Debug.LogWarning($"[ConfigTables] Table already exists: {tableName}");
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
        /// 获取内存统计信息
        /// </summary>
        public MemoryInfo GetMemoryInfo()
        {
            long pendingSize = 0;
            if (_pendingBytes != null)
            {
                foreach (var pair in _pendingBytes)
                {
                    pendingSize += pair.Value?.Length ?? 0;
                }
            }

            return new MemoryInfo
            {
                RawBytesSize = _rawBytes?.Length ?? 0,
                PendingBytesSize = pendingSize,
                IndexCount = _tableIndex?.Count ?? 0,
                LoadedTableCount = _loadedTables?.Count ?? 0
            };
        }

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
