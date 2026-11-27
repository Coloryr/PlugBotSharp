using System.Text.Json.Serialization;

namespace PlugBotSharp.Api.Configs;

/// <summary>
/// 配置项定义：Key 是配置名，子类带当前值和约束，交给网页端渲染成表单
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConfigIntMap), "int")]
[JsonDerivedType(typeof(ConfigShortMap), "short")]
[JsonDerivedType(typeof(ConfigUshortMap), "ushort")]
[JsonDerivedType(typeof(ConfigUintMap), "uint")]
[JsonDerivedType(typeof(ConfigLongMap), "long")]
[JsonDerivedType(typeof(ConfigUlongMap), "ulong")]
[JsonDerivedType(typeof(ConfigFloatMap), "float")]
[JsonDerivedType(typeof(ConfigDoubleMap), "double")]
[JsonDerivedType(typeof(ConfigBoolMap), "bool")]
[JsonDerivedType(typeof(ConfigStringMap), "string")]
[JsonDerivedType(typeof(ConfigListMap), "list")]
[JsonDerivedType(typeof(ConfigKvMap), "kv")]
[JsonDerivedType(typeof(ConfigEnumMap), "enum")]
public abstract record ConfigMap
{
    /// <summary>
    /// 配置名
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Key 的解释，面板上显示在输入框旁边
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 输入框占位符，为空时用默认值
    /// </summary>
    public string Placeholder { get; set; } = "";
}

/// <summary>
/// 整数配置
/// </summary>
public record ConfigIntMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public int Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public int Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public int Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public int Default { get; set; }
}

/// <summary>
/// 短整数配置
/// </summary>
public record ConfigShortMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public short Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public short Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public short Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public short Default { get; set; }
}

/// <summary>
/// 无符号短整数配置
/// </summary>
public record ConfigUshortMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public ushort Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public ushort Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public ushort Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public ushort Default { get; set; }
}

/// <summary>
/// 无符号整数配置
/// </summary>
public record ConfigUintMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public uint Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public uint Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public uint Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public uint Default { get; set; }
}

/// <summary>
/// 长整数配置
/// </summary>
public record ConfigLongMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public long Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public long Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public long Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public long Default { get; set; }
}

/// <summary>
/// 无符号长整数配置
/// </summary>
public record ConfigUlongMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public ulong Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public ulong Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public ulong Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public ulong Default { get; set; }
}

/// <summary>
/// 浮点数配置
/// </summary>
public record ConfigFloatMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public double Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public double Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public double Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public double Default { get; set; }
}

/// <summary>
/// 双精度小数配置
/// </summary>
public record ConfigDoubleMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public double Value { get; set; }
    /// <summary>
    /// 最小值
    /// </summary>
    public double Min { get; set; }
    /// <summary>
    /// 最大值
    /// </summary>
    public double Max { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public double Default { get; set; }
}

/// <summary>
/// 开关配置
/// </summary>
public record ConfigBoolMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public bool Value { get; set; }
    /// <summary>
    /// 默认值
    /// </summary>
    public bool Default { get; set; }
}

/// <summary>
/// 文本配置
/// </summary>
public record ConfigStringMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public string Value { get; set; } = "";
    /// <summary>
    /// 默认值
    /// </summary>
    public string Default { get; set; } = "";
    /// <summary>
    /// 最大长度，0 表示不限制
    /// </summary>
    public int MaxLength { get; set; }
    /// <summary>
    /// 密码框，网页端不明文回显
    /// </summary>
    public bool Secret { get; set; }
}

/// <summary>
/// 列表配置，每一项是一行文本
/// </summary>
public record ConfigListMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public List<string> Value { get; set; } = [];
    /// <summary>
    /// 默认值
    /// </summary>
    public List<string> Default { get; set; } = [];
    /// <summary>
    /// 最多几项，0 表示不限制
    /// </summary>
    public int MaxCount { get; set; }
}

/// <summary>
/// 键值对配置
/// </summary>
public record ConfigKvMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public Dictionary<string, string> Value { get; set; } = [];
    /// <summary>
    /// 默认值
    /// </summary>
    public Dictionary<string, string> Default { get; set; } = [];
}
/// <summary>
/// 单选配置，Options 是可选值
/// </summary>
public record ConfigEnumMap : ConfigMap
{
    /// <summary>
    /// 当前值
    /// </summary>
    public string Value { get; set; } = "";
    /// <summary>
    /// 默认值
    /// </summary>
    public string Default { get; set; } = "";
    /// <summary>
    /// 可选值
    /// </summary>
    public List<string> Options { get; set; } = [];
}