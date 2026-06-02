# T2FConfigTable

基于 Luban 的轻量级配置表框架，支持二进制文件合并、多种加载模式和编辑器工具。

## 特性

- **多种加载模式** - 立即加载、延迟加载、手动加载，适应不同场景
- **零拷贝加载** - 使用 ByteBuf 切片，避免内存复制，降低内存峰值
- **二进制合并** - 将多个 `.bytes` 文件合并为单个文件，减少加载次数
- **表级扩展钩子** - 生成的 `TbXxx` 自动暴露 `OnDeserialized` / `OnRefResolved` 两个 partial method，按需 opt-in，零开销
- **编辑器工具** - 可视化配置管理窗口

## 安装

### 通过 Package Manager 安装

**步骤 1：安装 Luban Unity 运行时库**

```
https://gitee.com/focus-creative-games/luban_unity.git
```

**步骤 2：安装 T2FConfigTable**

```
https://github.com/unittt/T2FConfigTable.git
```

## 快速开始

### 1. 生成配置表代码

使用 Luban 生成配置表代码，框架提供内置模板 `T2FConfigTable/Templates/cs-bin/tables.sbn`。

### 2. 配置合并规则

打开菜单 `T2F > Config Table Manager`，配置输入目录和输出文件。

### 3. 初始化配置表

**推荐方式（统一 API）：**

```csharp
// 立即加载（默认）
Tables.Init(mergedBytes);

// 延迟加载
Tables.Init(mergedBytes, true);
```

**立即加载模式：**

```csharp
Tables.InitImmediate(mergedBytes);
var item = Tables.Instance.TbItem[1001];
```

**延迟加载模式：**

```csharp
Tables.InitLazy(mergedBytes);
var item = Tables.Instance.TbItem[1001];  // 首次访问时加载

// 有跨表引用时手动解析
Tables.Instance.ResolveAllRefs();

// 所有表加载完成后释放索引缓存
if (Tables.Instance.PendingTableCount == 0)
    Tables.Instance.ClearPendingBytes();
```

**手动加载模式：**

```csharp
Tables.InitManual();
Tables.Instance.AddTableBytes("tbitem", itemBytes);
var item = Tables.Instance.TbItem[1001];
Tables.Instance.ResolveAllRefs();
```

**内存统计：**

```csharp
var info = Tables.Instance.GetMemoryInfo();
Debug.Log($"原始数据: {info.RawBytesSize / 1024}KB");
Debug.Log($"待加载: {info.PendingBytesSize / 1024}KB");
Debug.Log($"已加载表: {info.LoadedTableCount}");
```

## API 参考

```csharp
// 初始化
static bool Init(byte[] mergedBytes, bool lazy = false, bool resolveRefs = true)
static bool InitImmediate(byte[] mergedBytes, bool resolveRefs = true)
static bool InitImmediate(Dictionary<string, byte[]> bytesDic, bool resolveRefs = true)
static bool InitLazy(byte[] mergedBytes)
static bool InitLazy(Dictionary<string, byte[]> bytesDic)
static bool InitManual()

// 属性
static T Instance { get; }
bool IsInitialized { get; }
bool IsRefResolved { get; }
int LoadedTableCount { get; }
int PendingTableCount { get; }

// 方法
bool AddTableBytes(string tableName, byte[] bytes)  // 仅 Manual 模式
bool IsTableLoaded(string tableName)
bool IsTablePending(string tableName)
void ResolveAllRefs(bool force = false)
MemoryInfo GetMemoryInfo()
void ClearPendingBytes()
static void Release()
```

## 加载模式对比

| 模式 | 适用场景 | 特点 |
|------|---------|------|
| **Immediate** | 生产环境 | 一次性加载所有表，自动解析引用 |
| **Lazy** | 快速启动、按需加载 | 首次访问时加载 |
| **Manual** | 热更新、单表加载 | 完全手动控制 |

## 表级扩展钩子

每个生成的 `TbXxx` 类都包含两个 partial method 声明，允许用户在不修改生成代码的前提下注入自定义逻辑：

| 钩子 | 触发时机 | 可访问范围 |
|------|---------|------|
| `OnDeserialized()` | 表自身反序列化 + 索引构建完成 | 仅本表数据 |
| `OnRefResolved(Tables tables)` | 跨表引用解析完成 | 本表 + 其他已 resolve 的表 |

**示例：**

```csharp
// TbCharacterBean.Hook.cs（用户手写，与生成文件分离）
namespace GameCode.ConfigTable.GameModule
{
    public partial class TbCharacterBean
    {
        partial void OnDeserialized()
        {
            // 表内聚合，如按字段分组、构建衍生缓存
        }

        partial void OnRefResolved(Tables tables)
        {
            // 安全访问其他表，如建立跨表索引
            // 例：_byElementGroup = _dataList.GroupBy(c => tables.TbElementBean.Get(c.ElementId).Category)...
        }
    }
}
```

**特性：**

- 未实现时编译器移除调用点，**零运行时开销**
- 用户文件独立存放，**不会被 Luban 覆盖**
- `OnRefResolved` 触发时，仅遍历顺序在本表之前的表已 resolve 完毕；若需"全局 resolve 完成"语义，请在 `Tables.ResolveAllRefs` 之后处理

> 注：本特性依赖框架内置的 `Templates/cs-bin/table.sbn`，而非 Luban 默认模板。

## Luban 模板配置

```batch
set TEMPLATEDIR=%WORKSPACE%\Assets\T2FConfigTable\Templates

:: Package Manager 安装时使用动态查找：
:: for /d %%i in ("%WORKSPACE%\Library\PackageCache\com.t2f.configtable@*") do set TEMPLATEDIR=%%i\Templates

dotnet %LUBAN_DLL% ^
    -t client -c cs-bin -d bin ^
    --conf %CONF_ROOT%\luban.conf ^
    --customTemplateDir %TEMPLATEDIR% ^
    -x cs-bin.outputCodeDir=%OUTPUTCODEDIR% ^
    -x outputDataDir=%OUTPUTDATADIR%
```

> **模板覆盖说明**：`Templates/cs-bin/` 同时覆盖了 Luban 默认的两份模板：
> - `tables.sbn` —— 容器类（`Tables`），继承 `ConfigTablesBase<Tables>`，提供 `Init`/`InitLazy` 等加载入口
> - `table.sbn` —— 每张单表（`TbXxx`），在构造函数和 `ResolveRef` 末尾注入 [表级扩展钩子](#表级扩展钩子)
>
> 若使用自定义 Luban 模板,需保留这两份覆盖,否则扩展钩子会失效。

## 依赖

- [Luban Unity](https://gitee.com/focus-creative-games/luban_unity)
- [Luban](https://github.com/focus-creative-games/luban)

## 许可证

MIT License
