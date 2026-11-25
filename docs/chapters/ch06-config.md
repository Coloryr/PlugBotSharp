# 第六章：配置文件

组件和实例都可以把配置项声明出来，面板照着生成表单。存放位置是宿主的 `Configs/{组件id}`（实例是 `Instances/{uuid}`），**文件由组件自己读写**。

## 1. 声明配置项

在组件（或实例）上实现两个方法：

```csharp
public Dictionary<string, ConfigMap> ConfigMap() => new()
{
    ["LogMessage"] = new ConfigBoolMap
    {
        Key = "LogMessage",
        Value = _config.LogMessage,
        Default = false,
        Description = "把收发内容写进调试日志"
    },
    ["Port"] = new ConfigIntMap
    {
        Key = "Port",
        Value = _config.Port,
        Min = 1,
        Max = 65535,
        Default = 8080
    }
};

public void UpdateMap(Dictionary<string, ConfigMap> data)
{
    if (data.TryGetValue("LogMessage", out var log) && log is ConfigBoolMap logMap)
    {
        _config.LogMessage = logMap.Value;
    }

    if (data.TryGetValue("Port", out var port) && port is ConfigIntMap portMap)
    {
        _config.Port = portMap.Value;
    }

    SaveConfig();
}
```

`Key` 是面板上的字段名，`Value` 是当前值，`Default` 给「恢复默认值」按钮用，`Description` 显示在输入框上方。

## 2. 支持的类型

| `type` | 记录 | 面板控件 |
| --- | --- | --- |
| `int` | `ConfigIntMap` | 数字框，带 `Min` / `Max` |
| `short` / `ushort` / `uint` / `long` / `ulong` | `ConfigShortMap` / `ConfigUshortMap` / `ConfigUintMap` / `ConfigLongMap` / `ConfigUlongMap` | 整数框 |
| `float` / `double` | `ConfigFloatMap` / `ConfigDoubleMap` | 小数框 |
| `bool` | `ConfigBoolMap` | 开关 |
| `string` | `ConfigStringMap` | 文本框，`Secret = true` 是密码框，`MaxLength` 限长 |
| `list` | `ConfigListMap` | 多行文本，一行一项，`MaxCount` 限项数 |
| `kv` | `ConfigKvMap` | 键值对，可增删行 |
| `enum` | `ConfigEnumMap` | 下拉框，选项来自 `Options` |

## 3. 落盘

`Init(path)` 拿到的目录就是给你存配置的：

```csharp
private string _path = "";
private ConfigObj _config = new();

public bool Init(string path)
{
    _path = path;        // Configs/echo
    ReadConfig();

    return true;
}

private void ReadConfig()
{
    var file = Path.Combine(_path, "Config.json");

    _config = File.Exists(file)
        ? JsonSerializer.Deserialize<ConfigObj>(File.ReadAllText(file)) ?? new ConfigObj()
        : new ConfigObj();
}

private void SaveConfig()
{
    File.WriteAllText(Path.Combine(_path, "Config.json"),
        JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
}
```

- 宿主**不会**替你保存，`UpdateMap` 里必须自己写文件
- 读文件失败时回退默认值，不要让组件加载失败
- 密码类字段用 `Secret = true`，面板不回显明文

## 4. 什么时候生效

保存设置后面板会调 `POST .../reload`：

| 对象 | 重载顺序 |
| --- | --- |
| 组件 | `Disable → Reload → Enable` |
| 实例 | `Disable 实例 → Reload(实例) → Reload 实例 → Enable 实例 |

所以 `Reload()` 里重新读一遍配置就行，不用在 `UpdateMap` 里做重活。本来就没启用的组件，重载时只调 `Reload`，不会顺手启用。

## 5. 接口

| 路径 | 说明 |
| --- | --- |
| `GET /api/admin/components/{id}/config` | 读组件配置项 |
| `POST /api/admin/components/{id}/config` | 保存组件配置 |
| `GET /api/admin/instances/{uuid}/config` | 读实例配置项 |
| `POST /api/admin/instances/{uuid}/config` | 保存实例配置 |
| `POST .../reload` | 重载配置 |

序列化用独立的 `ConfigMapContext`，字段 camelCase，多态判别字段是 `type`。

## 6. 注意

- `ConfigMap()` 每次打开面板都会调，返回的是当场拼的快照，改了内部字段要让下次返回新值
- `UpdateMap` 收到的 `data` 是面板提交的同一批配置项，可能是全量，也可能是部分，按 Key 取
- 不要在 `ConfigMap()` 里做 IO，面板每次轮询都会调

---

上一章：[第五章 · 自定义事件](ch05-events.md) ｜ [返回目录](../README.md) ｜ 下一章：[第七章 · 自定义数据](ch07-state.md)
