namespace ServiceLib.Models;

public class ConnectionProcessModel
{
    // 通用字段
    public bool IsProcess { get; set; }  // true = 进程行, false = 连接行

    // 进程行专用
    public string? ProcessName { get; set; }
    public int ConnectionCount { get; set; }
    public bool IsExpanded { get; set; }
    public List<ConnectionProcessModel>? Children { get; set; }

    // 连接行专用（进程行为空）
    public string? Id { get; set; }
    public string? Host { get; set; }
    public string? Network { get; set; }
    public string? Type { get; set; }
    public string? Chain { get; set; }
    public string? Elapsed { get; set; }
    public ulong Upload { get; set; }
    public ulong Download { get; set; }
}
