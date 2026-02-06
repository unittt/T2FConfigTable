# T2FConfigTable

基于 Luban 的轻量级配置表框架，支持二进制文件合并、多种加载模式和编辑器工具。

## 特性

- **多种加载模式** - 立即加载、延迟加载、手动加载，适应不同场景
- **零拷贝延迟加载** - Lazy 模式使用 ByteBuf 切片，避免内存复制
- **二进制合并** - 将多个 `.bytes` 文件合并为单个文件，减少加载次数
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

// 所有表加载完成后释放缓存
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

## API 参考

```csharp
// 初始化
static bool InitImmediate(byte[] mergedBytes, bool resolveRefs = true)
static bool InitLazy(byte[] mergedBytes)
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
void ClearPendingBytes()
static void Release()
```

## 加载模式对比

| 模式 | 适用场景 | 特点 |
|------|---------|------|
| **Immediate** | 生产环境 | 一次性加载所有表，自动解析引用 |
| **Lazy** | 内存优化、快速启动 | 按需加载，零拷贝切片 |
| **Manual** | 热更新、单表加载 | 完全手动控制 |

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

## 依赖

- [Luban Unity](https://gitee.com/focus-creative-games/luban_unity)
- [Luban](https://github.com/focus-creative-games/luban)

## 许可证

MIT License
