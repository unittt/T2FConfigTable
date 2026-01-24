# T2FConfigTable

基于 Luban 的轻量级配置表框架，支持二进制文件合并、延迟加载、自动生成和编辑器工具。

## 特性

- **框架与业务解耦** - 生成的配置表代码放在项目中，框架可独立复用
- **泛型单例基类** - `ConfigTablesBase<T>` 提供统一的初始化和访问接口
- **延迟加载** - 支持按需加载单个表，减少初始化时间和内存占用
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
│   ├── ConfigTablesBase.cs     # 泛型单例基类（支持延迟加载）
│   └── BytesFileHandler.cs     # 二进制打包/解包
├── Editor/
│   ├── BytesFileMerger.cs      # 自动合并处理器
│   ├── MergeConfig.cs          # 合并配置
│   └── MergeConfigEditor.cs    # 编辑器窗口
└── Res/Gen/                    # 数据文件输出目录
```

## 快速开始

### 1. 生成配置表代码

使用 Luban 生成配置表代码，让 `Tables` 类继承 `ConfigTablesBase<Tables>`。

**使用自定义模板支持延迟加载：**

```csharp
// 生成的代码（放在项目中，不在框架内）
namespace T2F.ConfigTable
{
    public partial class Tables : ConfigTablesBase<Tables>
    {
        // 私有字段
        private GameModule.TbItemBean _TbItemBean;

        // 延迟加载属性
        public GameModule.TbItemBean TbItemBean =>
            _TbItemBean ?? LoadTableLazy(ref _TbItemBean, "tbitembean",
                bytes => new GameModule.TbItemBean(bytes));

        protected override void OnLoad(Func<string, ByteBuf> loader)
        {
            var TbItemBeanBytes = loader("tbitembean");
            if (TbItemBeanBytes != null)
                _TbItemBean = new GameModule.TbItemBean(TbItemBeanBytes);
        }

        protected override void OnResolveRef()
        {
            _TbItemBean?.ResolveRef(this);
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

**立即加载模式（生产环境推荐）：**

```csharp
// 加载合并后的二进制文件
byte[] mergedBytes = LoadMergedBytes();

// 初始化并立即加载所有表
Tables.Init(mergedBytes);

// 访问配置（已加载）
var item = Tables.Instance.TbItemBean[1001];
```

**延迟加载模式（开发调试/内存受限场景）：**

```csharp
// 加载合并后的二进制文件
byte[] mergedBytes = LoadMergedBytes();

// 延迟初始化（仅存储字节数据，不加载表）
Tables.InitLazy(mergedBytes);

// 访问配置（首次访问时自动加载该表）
var item = Tables.Instance.TbItemBean[1001];

// 查看加载状态
Debug.Log($"已加载: {Tables.Instance.LoadedTableCount}/{Tables.Instance.TotalTableCount}");

// 需要跨表引用时，手动解析引用
Tables.Instance.ResolveAllRefs();

// 节省内存：释放原始字节数据（之后无法再延迟加载新表）
Tables.Instance.ReleaseRawBytes();
```

## API 参考

### ConfigTablesBase<T>

```csharp
// ===== 静态属性 =====
public static T Instance { get; }           // 单例实例
public static bool IsInitialized { get; }   // 是否已初始化

// ===== 实例属性 =====
public bool IsLazyMode { get; }             // 是否为延迟加载模式
public bool IsRefResolved { get; }          // 是否已解析引用
public int LoadedTableCount { get; }        // 已加载的表数量
public int TotalTableCount { get; }         // 总表数量

// ===== 立即加载 =====
public static void Init(byte[] mergedBytes);           // 从合并字节初始化
public static void Init(Dictionary<string, byte[]>);   // 从字典初始化

// ===== 延迟加载 =====
public static void InitLazy(byte[] mergedBytes);       // 延迟初始化（从合并字节）
public static void InitLazy(Dictionary<string, byte[]>); // 延迟初始化（从字典）

// ===== 实例方法 =====
public bool IsTableLoaded(string tableName);  // 检查表是否已加载
public void LoadAllTables();                  // 加载所有未加载的表
public void ResolveAllRefs();                 // 解析所有引用关系
public void ReleaseRawBytes();                // 释放原始字节数据

// ===== 释放 =====
public static void Release();                 // 释放实例
```

### BytesFileHandler

```csharp
// 打包多个文件为单个二进制块（编辑器使用）
internal static byte[] PackBytes(Dictionary<string, byte[]> fileDict);

// 解包二进制块（运行时使用）
internal static Dictionary<string, byte[]> UnpackBytes(byte[] mergedBytes);
```

## 延迟加载使用场景

| 场景 | 推荐模式 | 说明 |
|------|----------|------|
| 生产环境 | `Init()` | 一次性加载所有表，避免运行时卡顿 |
| 开发调试 | `InitLazy()` | 快速启动，按需加载 |
| 内存受限设备 | `InitLazy()` | 减少内存占用 |
| 大型配置表 | `InitLazy()` | 分散加载压力 |

### 延迟加载注意事项

1. **引用解析**：延迟模式下 `ResolveRef` 不会自动调用。如果表之间有引用关系，需要在所有相关表加载后手动调用 `ResolveAllRefs()`。

2. **首次访问延迟**：延迟模式下，首次访问某个表会触发加载，可能产生微小延迟。

3. **内存管理**：调用 `ReleaseRawBytes()` 后无法再延迟加载新表，但可以节省内存。

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

## Luban 模板配置

框架提供自定义 Luban 模板以支持延迟加载。将模板放置在 `DataTables/Templates/cs-bin/tables.sbn`：

```
DataTables/
├── Templates/
│   └── cs-bin/
│       └── tables.sbn    # 自定义模板
├── luban.conf
└── gen - c#.bat
```

生成命令需包含 `--customTemplateDir` 参数：

```batch
dotnet Luban.dll ^
    -t client ^
    -c cs-bin ^
    --conf luban.conf ^
    --customTemplateDir Templates ^
    ...
```

## 配置文件位置

合并配置保存在：`ProjectSettings/T2FConfigTableSettings.asset`

## 依赖

- Unity 2022.3+
- Luban（用于生成配置表代码）

## 许可证

MIT License
