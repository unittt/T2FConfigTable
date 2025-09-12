using System.Collections.Generic;
using Luban;
using UnityEngine;

namespace T2F.ConfigTable
{
    public partial class Tables
    {

        /// <summary>
        /// 单例
        /// </summary>
        public static Tables Instance { get; private set; }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="bytesDic"> 字节数组字典 </param>
        public static void Init(Dictionary<string, byte[]> bytesDic)
        {
            if (bytesDic == null)
            {
                Debug.LogError("bytesDic is null");
                return;
            }
            
            Instance = new Tables((tableName) =>
            {
                var bytes = bytesDic[tableName];
                return new ByteBuf(bytes);
            });
        }
        
        /// <summary>
        /// 初始化 (合并后的字节数组)
        /// </summary>
        /// <param name="mergedBytes"> 合并后的字节数组 </param>
        public static void Init(byte[] mergedBytes)
        {
            var bytesDic = BytesFileHandler.UnpackBytes(mergedBytes);
            Init(bytesDic);
        }
    }
}