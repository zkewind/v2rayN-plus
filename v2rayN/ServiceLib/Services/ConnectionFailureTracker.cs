using System.Text.RegularExpressions;
using ServiceLib.Events;
using ServiceLib.Models;

namespace ServiceLib.Services;

public sealed class ConnectionFailureTracker
{
    private sealed class ConnectionSnapshot
    {
        public string? Network { get; init; }
        public string? Type { get; init; }
        public string? ProcessName { get; init; }
        public string? ProcessPath { get; init; }
        public DateTime LastSeen { get; init; }
    }

    private static readonly Lazy<ConnectionFailureTracker> _instance = new(() => new());
    private static readonly Regex _failureRegex = new(
        @"open connection to (?<target>.+?) using outbound/(?<outbound>[^\[]+)\[(?<final>[^\]]+)\]: (?<error>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly TimeSpan FailureRetention = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SnapshotRetention = TimeSpan.FromMinutes(2);

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, FailedConnectionItem> _failures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ConnectionSnapshot> _recentConnections = new(StringComparer.OrdinalIgnoreCase);

    public static ConnectionFailureTracker Instance => _instance.Value;

    public void ProcessLogLine(string? line)
    {
        if (line.IsNullOrEmpty())
        {
            return;
        }

        var match = _failureRegex.Match(line.Trim());
        if (!match.Success)
        {
            return;
        }

        var rawTarget = match.Groups["target"].Value.Trim();
        if (!TrySplitTarget(rawTarget, out var host, out var port))
        {
            host = rawTarget;
            port = null;
        }

        var key = BuildFailureKey(rawTarget, match.Groups["error"].Value.Trim());
        lock (_syncRoot)
        {
            TrimExpiredLocked();

            if (!_failures.TryGetValue(key, out var failure))
            {
                failure = new FailedConnectionItem
                {
                    Key = key,
                    RawTarget = rawTarget,
                };
                _failures[key] = failure;
            }

            failure.RawTarget = rawTarget;
            failure.TargetHost = host;
            failure.TargetPort = port;
            failure.OutboundTag = match.Groups["outbound"].Value.Trim();
            failure.FinalOutbound = match.Groups["final"].Value.Trim();
            failure.ErrorMessage = match.Groups["error"].Value.Trim();
            failure.LastSeen = DateTime.Now;

            TryEnrichFailureLocked(failure);
        }

        AppEvents.ConnectionFailuresChanged.Publish(Unit.Default);
    }

    public void UpdateActiveConnections(IEnumerable<ConnectionItem>? connections)
    {
        var changed = false;
        lock (_syncRoot)
        {
            TrimExpiredLocked();

            foreach (var item in connections ?? [])
            {
                var metadata = item.metadata;
                if (metadata == null || metadata.destinationPort.IsNullOrEmpty())
                {
                    continue;
                }

                var snapshot = new ConnectionSnapshot
                {
                    Network = metadata.network,
                    Type = metadata.type,
                    ProcessName = GetProcessName(metadata),
                    ProcessPath = metadata.processPath,
                    LastSeen = DateTime.Now,
                };

                foreach (var endpoint in EnumerateEndpoints(metadata))
                {
                    _recentConnections[endpoint] = snapshot;
                }
            }

            foreach (var failure in _failures.Values)
            {
                changed |= TryEnrichFailureLocked(failure);
            }
        }

        if (changed)
        {
            AppEvents.ConnectionFailuresChanged.Publish(Unit.Default);
        }
    }

    public List<FailedConnectionItem> GetRecentFailures()
    {
        lock (_syncRoot)
        {
            TrimExpiredLocked();
            return _failures.Values
                .OrderByDescending(x => x.LastSeen)
                .Select(CloneFailure)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _failures.Clear();
            _recentConnections.Clear();
        }

        AppEvents.ConnectionFailuresChanged.Publish(Unit.Default);
    }

    private static FailedConnectionItem CloneFailure(FailedConnectionItem item)
    {
        return new FailedConnectionItem
        {
            Key = item.Key,
            TargetHost = item.TargetHost,
            TargetPort = item.TargetPort,
            RawTarget = item.RawTarget,
            Network = item.Network,
            Type = item.Type,
            ProcessName = item.ProcessName,
            ProcessPath = item.ProcessPath,
            OutboundTag = item.OutboundTag,
            FinalOutbound = item.FinalOutbound,
            ErrorMessage = item.ErrorMessage,
            LastSeen = item.LastSeen,
        };
    }

    private static string BuildFailureKey(string rawTarget, string error)
    {
        return $"{rawTarget}|{error}";
    }

    private static string? GetProcessName(MetadataItem metadata)
    {
        if (metadata.process.IsNotEmpty())
        {
            return metadata.process;
        }

        if (metadata.processPath.IsNullOrEmpty())
        {
            return null;
        }

        var path = metadata.processPath.Replace('/', '\\');
        var lastSlash = path.LastIndexOf('\\');
        return lastSlash >= 0 && lastSlash < path.Length - 1
            ? path[(lastSlash + 1)..]
            : null;
    }

    private static IEnumerable<string> EnumerateEndpoints(MetadataItem metadata)
    {
        if (metadata.destinationPort.IsNullOrEmpty())
        {
            yield break;
        }

        if (metadata.host.IsNotEmpty())
        {
            yield return $"{metadata.host}:{metadata.destinationPort}";
        }

        if (metadata.destinationIP.IsNotEmpty())
        {
            yield return $"{metadata.destinationIP}:{metadata.destinationPort}";
        }

        if (metadata.remoteDestination.IsNotEmpty())
        {
            yield return metadata.remoteDestination;
        }
    }

    private bool TryEnrichFailureLocked(FailedConnectionItem failure)
    {
        var enriched = false;
        foreach (var endpoint in EnumerateFailureEndpoints(failure))
        {
            if (!_recentConnections.TryGetValue(endpoint, out var snapshot))
            {
                continue;
            }

            if (failure.Network.IsNullOrEmpty() && snapshot.Network.IsNotEmpty())
            {
                failure.Network = snapshot.Network;
                enriched = true;
            }

            if (failure.Type.IsNullOrEmpty() && snapshot.Type.IsNotEmpty())
            {
                failure.Type = snapshot.Type;
                enriched = true;
            }

            if (failure.ProcessName.IsNullOrEmpty() && snapshot.ProcessName.IsNotEmpty())
            {
                failure.ProcessName = snapshot.ProcessName;
                enriched = true;
            }

            if (failure.ProcessPath.IsNullOrEmpty() && snapshot.ProcessPath.IsNotEmpty())
            {
                failure.ProcessPath = snapshot.ProcessPath;
                enriched = true;
            }
        }

        return enriched;
    }

    private static IEnumerable<string> EnumerateFailureEndpoints(FailedConnectionItem failure)
    {
        if (failure.RawTarget.IsNotEmpty())
        {
            yield return failure.RawTarget;
        }

        if (failure.TargetHost.IsNotEmpty() && failure.TargetPort.IsNotEmpty())
        {
            yield return $"{failure.TargetHost}:{failure.TargetPort}";
        }
    }

    private void TrimExpiredLocked()
    {
        var now = DateTime.Now;

        foreach (var key in _failures
                     .Where(x => now - x.Value.LastSeen > FailureRetention)
                     .Select(x => x.Key)
                     .ToList())
        {
            _failures.Remove(key);
        }

        foreach (var key in _recentConnections
                     .Where(x => now - x.Value.LastSeen > SnapshotRetention)
                     .Select(x => x.Key)
                     .ToList())
        {
            _recentConnections.Remove(key);
        }
    }

    private static bool TrySplitTarget(string rawTarget, out string? host, out string? port)
    {
        host = null;
        port = null;
        if (rawTarget.IsNullOrEmpty())
        {
            return false;
        }

        if (rawTarget[0] == '[')
        {
            var index = rawTarget.LastIndexOf("]:", StringComparison.Ordinal);
            if (index > 0)
            {
                host = rawTarget[1..index];
                port = rawTarget[(index + 2)..];
                return true;
            }
        }

        var separator = rawTarget.LastIndexOf(':');
        if (separator <= 0 || separator >= rawTarget.Length - 1)
        {
            return false;
        }

        host = rawTarget[..separator];
        port = rawTarget[(separator + 1)..];
        return true;
    }
}
