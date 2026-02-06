using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Luban;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 表索引信息
    /// </summary>
    internal readonly struct TableIndex
    {
        public readonly int Offset;
        public readonly int Size;

        public TableIndex(int offset, int size)
        {
            Offset = offset;
            Size = size;
        }
    }

    /// <summary>
    /// 二进制文件处理（v3 格式，支持流式解包）
    /// </summary>
    /// <remarks>
    /// 格式结构：
    /// [Header: "BYTES_v3"]
    /// [FileCount: int32]
    /// [IndexSize: int32]
    /// [索引区: (NameLength + Name + Offset + Size) * N]
    /// [数据区: Content * N]
    /// </remarks>
    internal static class BytesFileHandler
    {
        private const string FileHeader = "BYTES_v3";

        /// <summary>
        /// 解析索引（不解包数据，内存友好）
        /// </summary>
        internal static Dictionary<string, TableIndex> ParseIndex(byte[] mergedBytes)
        {
            using var ms = new MemoryStream(mergedBytes, writable: false);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            string header = reader.ReadString();
            if (header != FileHeader)
                throw new InvalidDataException($"Invalid file header. Expected: {FileHeader}, Actual: {header}");

            int fileCount = reader.ReadInt32();
            int indexSize = reader.ReadInt32();

            var indexDict = new Dictionary<string, TableIndex>(fileCount);

            for (int i = 0; i < fileCount; i++)
            {
                int nameLen = reader.ReadInt32();
                string name = Encoding.UTF8.GetString(mergedBytes, (int)ms.Position, nameLen);
                ms.Position += nameLen;

                int offset = reader.ReadInt32();
                int size = reader.ReadInt32();

                indexDict[name] = new TableIndex(offset, size);
            }

            return indexDict;
        }

        /// <summary>
        /// 按需切片单个表数据（零拷贝）
        /// </summary>
        internal static ByteBuf SliceTable(byte[] mergedBytes, TableIndex index)
        {
            return new ByteBuf(mergedBytes, index.Offset, index.Offset + index.Size);
        }

        /// <summary>
        /// 打包为 v3 格式（编辑器使用）
        /// </summary>
        internal static byte[] PackBytes(Dictionary<string, byte[]> fileDict)
        {
            // 预计算索引区大小
            int indexSize = 0;
            foreach (var pair in fileDict)
            {
                indexSize += 4 + Encoding.UTF8.GetByteCount(pair.Key); // nameLen + name
                indexSize += 4 + 4; // offset + size
            }

            // 计算头部大小: BinaryWriter.Write(string) 写入 7bit 编码长度 + 字符串
            // "BYTES_v3" = 8 字节，长度前缀 1 字节，共 9 字节
            int headerSize = 1 + FileHeader.Length + 4 + 4; // lengthPrefix + header + count + indexSize

            // 数据区起始偏移
            int dataStartOffset = headerSize + indexSize;

            // 计算总大小
            int totalDataSize = 0;
            foreach (var pair in fileDict)
                totalDataSize += pair.Value.Length;

            int totalSize = dataStartOffset + totalDataSize;

            using var ms = new MemoryStream(totalSize);
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            // 写入头部
            writer.Write(FileHeader);
            writer.Write(fileDict.Count);
            writer.Write(indexSize);

            // 写入索引区（先计算所有偏移）
            int currentOffset = dataStartOffset;
            foreach (var pair in fileDict)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(pair.Key);
                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);
                writer.Write(currentOffset);
                writer.Write(pair.Value.Length);

                currentOffset += pair.Value.Length;
            }

            // 写入数据区
            foreach (var pair in fileDict)
            {
                writer.Write(pair.Value);
            }

            return ms.ToArray();
        }
    }
}
