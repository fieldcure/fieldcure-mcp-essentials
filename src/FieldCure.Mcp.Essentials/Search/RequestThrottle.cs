namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Enforces a minimum delay between outgoing requests to avoid rate-limiting
/// by search engines that rely on HTML scraping.
/// Thread-safe: concurrent callers are serialized via semaphore.
/// </summary>
internal sealed class RequestThrottle
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minInterval;
    private DateTime _lastRequest = DateTime.MinValue;

    /// <summary>
    /// Creates a new throttle with the specified minimum interval between requests.
    /// </summary>
    public RequestThrottle(TimeSpan minInterval)
    {
        _minInterval = minInterval;
    }

    /// <summary>
    /// Waits until the minimum interval has elapsed since the last request,
    /// then marks the current time as the latest request.
    /// </summary>
    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequest;
            if (elapsed < _minInterval)
            {
                var delay = _minInterval - elapsed;
                await Task.Delay(delay, ct);
            }
            _lastRequest = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }
}
