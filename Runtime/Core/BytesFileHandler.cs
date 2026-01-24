using System.Collections.Generic;
using System.IO;
using System.Text;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 二进制文件处理
    /// </summary>
    internal static class BytesFileHandler
    {
        private const string FileHeader = "BYTES_v2";

        /// <summary>
        /// 将多个文件内容合并为单一二进制块（编辑器使用）
        /// </summary>
        internal static byte[] PackBytes(Dictionary<string, byte[]> fileDict)
        {
            // 预计算总大小，避免 MemoryStream 多次扩容
            int totalSize = FileHeader.Length + 1 + 4; // header + count
            foreach (var pair in fileDict)
            {
                totalSize += 4 + Encoding.UTF8.GetByteCount(pair.Key); // nameLen + name
                totalSize += 4 + pair.Value.Length; // contentLen + content
            }

            using (var ms = new MemoryStream(totalSize))
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(FileHeader);
                writer.Write(fileDict.Count);

                foreach (var pair in fileDict)
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(pair.Key);
                    writer.Write(nameBytes.Length);
                    writer.Write(nameBytes);
                    writer.Write(pair.Value.Length);
                    writer.Write(pair.Value);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 解包合并的二进制块（运行时使用）
        /// </summary>
        internal static Dictionary<string, byte[]> UnpackBytes(byte[] mergedBytes)
        {
            using (var ms = new MemoryStream(mergedBytes, writable: false))
            using (var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true))
            {
                // 校验文件头
                string header = reader.ReadString();
                if (header != FileHeader)
                    throw new InvalidDataException($"Invalid file header. Expected:{FileHeader}, Actual:{header}");

                // 读取文件数量并预分配字典容量
                int fileCount = reader.ReadInt32();
                var fileDict = new Dictionary<string, byte[]>(fileCount);

                for (int i = 0; i < fileCount; i++)
                {
                    int nameLength = reader.ReadInt32();
                    string fileName = Encoding.UTF8.GetString(mergedBytes, (int)ms.Position, nameLength);
                    ms.Position += nameLength;

                    int contentLength = reader.ReadInt32();
                    byte[] content = reader.ReadBytes(contentLength);

                    fileDict[fileName] = content;
                }

                return fileDict;
            }
        }
    }
}
