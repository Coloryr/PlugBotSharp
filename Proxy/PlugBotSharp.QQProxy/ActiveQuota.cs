using PlugBotSharp.Core.Configs;

namespace PlugBotSharp.QQProxy;

/// <summary>
/// 主动消息配额：每个群每天最多 1000 条，记在实例目录的 quota.json
/// </summary>
internal sealed class ActiveQuota
{
    /// <summary>
    /// 每个群每天最多发这么多条主动消息
    /// </summary>
    public const int Limit = 1000;

    private readonly string _file;

    private QuotaFileObj _data = new();

    /// <summary>
    /// 创建配额记录
    /// </summary>
    /// <param name="folder">实例目录</param>
    public ActiveQuota(string folder)
    {
        _file = Path.Combine(folder, "quota.json");

        Load();
    }

    /// <summary>
    /// 这个群今天还能不能发
    /// </summary>
    /// <param name="group">群 openid</param>
    public bool CanSend(string group)
    {
        EnsureToday();

        return Get(group) < Limit;
    }

    /// <summary>
    /// 记一条已发出的主动消息
    /// </summary>
    /// <param name="group">群 openid</param>
    public void Add(string group)
    {
        EnsureToday();

        _data.Groups[group] = Get(group) + 1;

        Save();
    }

    /// <summary>
    /// 取某个群今天的条数
    /// </summary>
    /// <param name="group">群 openid</param>
    public int Get(string group)
    {
        return _data.Groups.TryGetValue(group, out var count) ? count : 0;
    }

    /// <summary>
    /// 取今天各群已发条数的快照，顺手处理跨天清零
    /// </summary>
    public IReadOnlyDictionary<string, int> Snapshot()
    {
        EnsureToday();

        return new Dictionary<string, int>(_data.Groups);
    }

    /// <summary>
    /// 跨天就把计数清零，清零后立刻写盘；返回这次是不是真的跨天了
    /// </summary>
    public bool RolloverIfNeeded()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        if (_data.Date == today)
        {
            return false;
        }

        _data.Date = today;
        _data.Groups.Clear();

        Save();

        return true;
    }

    /// <summary>
    /// 跨天就把计数清零
    /// </summary>
    private void EnsureToday()
    {
        RolloverIfNeeded();
    }

    /// <summary>
    /// 读配额文件，坏了就当空的
    /// </summary>
    private void Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _data = new QuotaFileObj { Date = DateTime.Now.ToString("yyyy-MM-dd") };

                return;
            }

            _data = JsonUtils.ToObj(File.ReadAllText(_file), JsonContext.Default.QuotaFileObj) ?? new QuotaFileObj();

            if (string.IsNullOrEmpty(_data.Date))
            {
                _data.Date = DateTime.Now.ToString("yyyy-MM-dd");
            }
        }
        catch (Exception e)
        {
            ProxyInstance.Logger?.Error(e, "主动消息配额文件读取失败");

            _data = new QuotaFileObj { Date = DateTime.Now.ToString("yyyy-MM-dd") };
        }
    }

    /// <summary>
    /// 写回配额文件
    /// </summary>
    private void Save()
    {
        try
        {
            File.WriteAllText(_file, JsonUtils.ToString(_data, JsonContext.Default.QuotaFileObj));
        }
        catch (Exception e)
        {
            ProxyInstance.Logger?.Error(e, "主动消息配额文件保存失败");
        }
    }
}

/// <summary>
/// 配额文件结构：某一天的各个群已发条数
/// </summary>
internal sealed class QuotaFileObj
{
    /// <summary>
    /// 日期 yyyy-MM-dd
    /// </summary>
    public string Date { get; set; } = "";

    /// <summary>
    /// 群 openid -&gt; 已发条数
    /// </summary>
    public Dictionary<string, int> Groups { get; set; } = [];
}
