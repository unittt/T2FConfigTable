# T2FConfigTable

基于 Luban 的轻量级配置表框架，支持二进制文件合并、自动生成和编辑器工具。

## 特性

- **框架与业务解耦** - 生成的配置表代码放在项目中，框架可独立复用
- **泛型单例基类** - `ConfigTablesBase<T>` 提供统一的初始化和访问接口
- **二进制合并** - 将多个 `.bytes` 文件合并为单个文件，减少加载次数
- **自动生成** - 监听文件变化，自动触发合并
- **编辑器工具** - 可视化配置管理窗口
- **增量检测** - MD5 哈希检测，跳过未变化的文件

## 目录结构

```
T2FConfigTable/
├── Plugins/LubanLib/           # Luban 运行时库
│   ├── BeanBase.cs
│   ├── ByteBuf.cs
│   ├── ITypeId.cs
│   └── StringUtil.cs
├── Runtime/Core/
│   ├── ConfigTablesBase.cs     # 泛型单例基类
│   └── BytesFileHandler.cs     # 二进制打包/解包
├── Editor/
│   ├── BytesFileMerger.cs      # 自动合并处理器
│   ├── MergeConfig.cs          # 合并配置
│   └── MergeConfigEditor.cs    # 编辑器窗口
└── Res/Gen/                    # 数据文件输出目录
```

## 快速开始

### 1. 生成配置表代码

使用 Luban 生成配置表代码，让 `Tables` 类继承 `ConfigTablesBase<Tables>`：

```csharp
// 生成的代码（放在项目中，不在框架内）
namespace T2F.ConfigTable
{
    public partial class Tables : ConfigTablesBase<Tables>
    {
        public GameModule.TbItemBean TbItemBean { get; private set; }
        // ... 其他配置表

        protected override void OnLoad(Func<string, ByteBuf> loader)
        {
            TbItemBean = new GameModule.TbItemBean(loader("tbitembean"));
            // ... 加载其他表
        }

        protected override void OnResolveRef()
        {
            TbItemBean.ResolveRef(this);
            // ... 解析引用
        }
    }
}
```

### 2. 配置合并规则

打开菜单 `T2F > Config Table Manager`，添加合并配置：

| 字段 | 说明 |
|------|------|
| 名称 | 配置项名称（用于标识） |
| 输入目录 | 包含 `.bytes` 文件的目录 |
| 输出文件 | 合并后的输出文件路径 |

### 3. 初始化配置表

```csharp
// 加载合并后的二进制文件
byte[] mergedBytes = LoadMergedBytes(); // 由业务层实现加载逻辑

// 初始化配置表
Tables.Init(mergedBytes);

// 访问配置
var item = Tables.Instance.TbItemBean[1001];
var name = item.Name;
```

## API 参考

### ConfigTablesBase<T>

```csharp
// 单例实例
public static T Instance { get; }

// 是否已初始化
public static bool IsInitialized { get; }

// 使用合并后的字节数组初始化
public static void Init(byte[] mergedBytes);

// 使用字典初始化（表名 -> 字节数组）
public static void Init(Dictionary<string, byte[]> bytesDic);

// 释放实例
public static void Release();
```

### BytesFileHandler

```csharp
// 打包多个文件为单个二进制块（编辑器使用）
internal static byte[] PackBytes(Dictionary<string, byte[]> fileDict);

// 解包二进制块（运行时使用）
internal static Dictionary<string, byte[]> UnpackBytes(byte[] mergedBytes);
```

## 编辑器工具

### Config Table Manager

菜单路径：`T2F > Config Table Manager`

功能：
- 添加/删除/排序合并配置
- 单项合并或全部合并
- 显示文件状态和更新时间
- 自动生成开关

### 自动合并

启用"自动生成"后，当输入目录中的 `.bytes` 文件发生变化时，会自动触发合并。

## 配置文件位置

合并配置保存在：`ProjectSettings/T2FConfigTableSettings.asset`

## 依赖

- Unity 2022.3+
- Luban（用于生成配置表代码）

## 许可证

MIT License
