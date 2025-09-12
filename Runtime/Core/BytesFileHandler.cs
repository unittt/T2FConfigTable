using System.Collections.Generic;
using System.IO;

namespace T2F.ConfigTable
{
    /// <summary>
    /// 二进制文件处理
    /// </summary>
    internal static class BytesFileHandler
    {
        // 合并标识头（可自定义）
        private const string FileHeader = "BYTES_v2";
        
        /// <summary>
        /// 将多个文件内容合并为单一二进制块
        /// </summary>
        /// <param name="fileDict">Key: 文件名, Value: 文件内容</param>
        /// <returns>合并后的字节数组</returns>
        internal static byte[] PackBytes(Dictionary<string, byte[]> fileDict)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // 写入文件头
                writer.Write(FileHeader);
                
                // 写入文件数量
                writer.Write(fileDict.Count);
                
                // 写入每个文件
                foreach (var pair in fileDict)
                {
                    // 文件名（UTF8编码）
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(pair.Key);
                    writer.Write(nameBytes.Length);
                    writer.Write(nameBytes);
                    
                    // 文件内容
                    writer.Write(pair.Value.Length);
                    writer.Write(pair.Value);
                }
                
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 解包合并的二进制块
        /// </summary>
        /// <param name="mergedBytes">合并后的字节数组</param>
        /// <returns>文件名到内容的字典</returns>
        internal static Dictionary<string, byte[]> UnpackBytes(byte[] mergedBytes)
        {
            var fileDict = new Dictionary<string, byte[]>();
            using (var ms = new MemoryStream(mergedBytes))
            using (var reader = new BinaryReader(ms))
            {
                // 校验文件头
                string header = reader.ReadString();
                if (header != FileHeader)
                    throw new InvalidDataException($"Invalid file header. Expected:{FileHeader}, Actual:{header}");
                
                // 读取文件数量
                int fileCount = reader.ReadInt32();
                
                for (int i = 0; i < fileCount; i++)
                {
                    // 读取文件名
                    int nameLength = reader.ReadInt32();
                    byte[] nameBytes = reader.ReadBytes(nameLength);
                    string fileName = System.Text.Encoding.UTF8.GetString(nameBytes);
                    
                    // 读取文件内容
                    int contentLength = reader.ReadInt32();
                    byte[] content = reader.ReadBytes(contentLength);
                    
                    fileDict[fileName] = content;
                }
            }
            return fileDict;
        }
    }
}