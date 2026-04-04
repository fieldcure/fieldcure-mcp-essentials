namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Enforces a randomized delay between outgoing requests to avoid rate-limiting
/// by search engines that rely on HTML scraping.
/// Thread-safe: concurrent callers are serialized via semaphore.
/// </summary>
internal sealed class RequestThrottle
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private DateTime _lastRequest = DateTime.MinValue;

    /// <summary>
    /// Creates a new throttle with a randomized delay range between requests.
    /// </summary>
    public RequestThrottle(TimeSpan minInterval, TimeSpan maxInterval)
    {
        _minDelayMs = (int)minInterval.TotalMilliseconds;
        _maxDelayMs = (int)maxInterval.TotalMilliseconds;
    }

    /// <summary>
    /// Waits until a randomized interval has elapsed since the last request,
    /// then marks the current time as the latest request.
    /// </summary>
    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var targetDelay = TimeSpan.FromMilliseconds(Random.Shared.Next(_minDelayMs, _maxDelayMs));
            var elapsed = DateTime.UtcNow - _lastRequest;
            if (elapsed < targetDelay)
                await Task.Delay(targetDelay - elapsed, ct);

            _lastRequest = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }
}
