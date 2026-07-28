using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Velopack.Sources;

namespace BetterClipboard.Services;

public readonly record struct DownloadTransferProgress(
    long BytesReceived,
    long? TotalBytes,
    double BytesPerSecond);

public sealed class AcceleratedFileDownloader : IFileDownloader
{
    private const int SegmentCount = 4;
    private const long SegmentedDownloadThreshold = 8 * 1024 * 1024;
    private const int BufferSize = 256 * 1024;

    private readonly HttpClientFileDownloader _fallback = new();

    public event Action<DownloadTransferProgress>? TransferProgressChanged;

    public string? DownloadDirectory { get; set; }

    public Task<byte[]> DownloadBytes(
        string url,
        IDictionary<string, string>? headers = null,
        double timeout = 30)
    {
        return _fallback.DownloadBytes(url, headers, timeout);
    }

    public Task<string> DownloadString(
        string url,
        IDictionary<string, string>? headers = null,
        double timeout = 30)
    {
        return _fallback.DownloadString(url, headers, timeout);
    }

    public async Task DownloadFile(
        string url,
        string targetFile,
        Action<int> progress,
        IDictionary<string, string>? headers = null,
        double timeout = 30,
        CancellationToken cancelToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        if (timeout > 0)
        {
            timeoutSource.CancelAfter(TimeSpan.FromMinutes(timeout));
        }

        using var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            MaxConnectionsPerServer = SegmentCount,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        using var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        var token = timeoutSource.Token;
        var downloadTarget = GetDownloadTargetPath(targetFile);
        var remoteFile = await ProbeRemoteFileAsync(client, url, headers, token);
        if (remoteFile.Length is > 0 &&
            File.Exists(downloadTarget) &&
            new FileInfo(downloadTarget).Length >= remoteFile.Length.Value)
        {
            File.Delete(downloadTarget);
        }

        var downloadedInSegments = false;
        if (remoteFile.SupportsRanges &&
            remoteFile.Length is >= SegmentedDownloadThreshold)
        {
            try
            {
                await DownloadInSegmentsAsync(
                    client,
                    url,
                    downloadTarget,
                    remoteFile.Length.Value,
                    progress,
                    headers,
                    token);
                downloadedInSegments = true;
            }
            catch (RangeDownloadNotSupportedException)
            {
                DeleteSegmentFiles(downloadTarget);
            }
        }

        if (!downloadedInSegments)
        {
            await DownloadSequentiallyAsync(
                client,
                url,
                downloadTarget,
                progress,
                headers,
                token);
        }

        await CopyToVelopackTargetAsync(downloadTarget, targetFile, token);
    }

    private async Task<RemoteFileInfo> ProbeRemoteFileAsync(
        HttpClient client,
        string url,
        IDictionary<string, string>? headers,
        CancellationToken cancelToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Head, url, headers);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancelToken);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            var supportsRanges = response.Headers.AcceptRanges.Any(
                value => string.Equals(value, "bytes", StringComparison.OrdinalIgnoreCase));
            return new RemoteFileInfo(response.Content.Headers.ContentLength, supportsRanges);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or TaskCanceledException &&
            !cancelToken.IsCancellationRequested)
        {
            return default;
        }
    }

    private async Task DownloadInSegmentsAsync(
        HttpClient client,
        string url,
        string targetFile,
        long totalBytes,
        Action<int> progress,
        IDictionary<string, string>? headers,
        CancellationToken cancelToken)
    {
        DeleteSegmentFiles(targetFile);
        var reporter = new TransferProgressReporter(
            totalBytes,
            progress,
            snapshot => TransferProgressChanged?.Invoke(snapshot));
        reporter.Report(0);

        var segmentSize = (long)Math.Ceiling(totalBytes / (double)SegmentCount);
        var tasks = new List<Task>(SegmentCount);
        for (var index = 0; index < SegmentCount; index++)
        {
            var start = index * segmentSize;
            if (start >= totalBytes)
            {
                break;
            }

            var end = Math.Min(totalBytes - 1, start + segmentSize - 1);
            var segmentPath = GetSegmentPath(targetFile, index);
            tasks.Add(DownloadSegmentWithRetryAsync(
                client,
                url,
                segmentPath,
                start,
                end,
                reporter,
                headers,
                cancelToken));
        }

        try
        {
            await Task.WhenAll(tasks);

            await using var destination = new FileStream(
                targetFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            for (var index = 0; index < tasks.Count; index++)
            {
                await using var segment = new FileStream(
                    GetSegmentPath(targetFile, index),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await segment.CopyToAsync(destination, cancelToken);
            }

            await destination.FlushAsync(cancelToken);
            reporter.Complete();
        }
        finally
        {
            DeleteSegmentFiles(targetFile);
        }
    }

    private static async Task DownloadSegmentWithRetryAsync(
        HttpClient client,
        string url,
        string segmentPath,
        long start,
        long end,
        TransferProgressReporter reporter,
        IDictionary<string, string>? headers,
        CancellationToken cancelToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            long attemptBytes = 0;
            try
            {
                await DownloadSegmentAsync(
                    client,
                    url,
                    segmentPath,
                    start,
                    end,
                    bytesRead =>
                    {
                        Interlocked.Add(ref attemptBytes, bytesRead);
                        reporter.Report(bytesRead);
                    },
                    headers,
                    cancelToken);
                return;
            }
            catch (RangeDownloadNotSupportedException)
            {
                reporter.Rollback(Interlocked.Read(ref attemptBytes));
                throw;
            }
            catch (Exception exception) when (
                attempt < 3 &&
                exception is HttpRequestException or IOException or TaskCanceledException &&
                !cancelToken.IsCancellationRequested)
            {
                reporter.Rollback(Interlocked.Read(ref attemptBytes));
                if (File.Exists(segmentPath))
                {
                    File.Delete(segmentPath);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancelToken);
            }
            catch
            {
                reporter.Rollback(Interlocked.Read(ref attemptBytes));
                throw;
            }
        }
    }

    private static async Task DownloadSegmentAsync(
        HttpClient client,
        string url,
        string segmentPath,
        long start,
        long end,
        Action<int> reportBytes,
        IDictionary<string, string>? headers,
        CancellationToken cancelToken)
    {
        using var request = CreateRequest(HttpMethod.Get, url, headers);
        request.Headers.Range = new RangeHeaderValue(start, end);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancelToken);
        if (response.StatusCode != HttpStatusCode.PartialContent ||
            response.Content.Headers.ContentRange?.From != start)
        {
            throw new RangeDownloadNotSupportedException();
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancelToken);
        await using var destination = new FileStream(
            segmentPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[BufferSize];
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancelToken);
            if (bytesRead == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancelToken);
            reportBytes(bytesRead);
        }

        if (destination.Length != end - start + 1)
        {
            throw new IOException(
                $"Segment length mismatch. Expected {end - start + 1}, received {destination.Length}.");
        }
    }

    private async Task DownloadSequentiallyAsync(
        HttpClient client,
        string url,
        string targetFile,
        Action<int> progress,
        IDictionary<string, string>? headers,
        CancellationToken cancelToken)
    {
        var existingBytes = File.Exists(targetFile) ? new FileInfo(targetFile).Length : 0;
        using var request = CreateRequest(HttpMethod.Get, url, headers);
        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancelToken);
        response.EnsureSuccessStatusCode();

        var isResume = existingBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        var totalBytes = isResume
            ? response.Content.Headers.ContentRange?.Length
            : response.Content.Headers.ContentLength;
        if (!isResume)
        {
            existingBytes = 0;
        }

        var reporter = new TransferProgressReporter(
            totalBytes,
            progress,
            snapshot => TransferProgressChanged?.Invoke(snapshot),
            existingBytes);
        reporter.Report(0);

        await using var source = await response.Content.ReadAsStreamAsync(cancelToken);
        await using var destination = new FileStream(
            targetFile,
            isResume ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[BufferSize];
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancelToken);
            if (bytesRead == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancelToken);
            reporter.Report(bytesRead);
        }

        await destination.FlushAsync(cancelToken);
        reporter.Complete();
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        IDictionary<string, string>? headers)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.AcceptEncoding.Clear();
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        foreach (var header in headers ?? new Dictionary<string, string>())
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }

    private string GetDownloadTargetPath(string velopackTargetFile)
    {
        if (string.IsNullOrWhiteSpace(DownloadDirectory))
        {
            return velopackTargetFile;
        }

        var directory = Path.GetFullPath(DownloadDirectory);
        Directory.CreateDirectory(directory);
        var fileName = Path.GetFileName(velopackTargetFile);
        if (fileName.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^".partial".Length];
        }

        return Path.Combine(directory, fileName);
    }

    private static async Task CopyToVelopackTargetAsync(
        string downloadedFile,
        string velopackTargetFile,
        CancellationToken cancelToken)
    {
        if (string.Equals(
                Path.GetFullPath(downloadedFile),
                Path.GetFullPath(velopackTargetFile),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var targetDirectory = Path.GetDirectoryName(velopackTargetFile);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        await using var source = new FileStream(
            downloadedFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            velopackTargetFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, cancelToken);
        await destination.FlushAsync(cancelToken);
    }

    private static string GetSegmentPath(string targetFile, int index)
    {
        return $"{targetFile}.segment-{index}";
    }

    private static void DeleteSegmentFiles(string targetFile)
    {
        for (var index = 0; index < SegmentCount; index++)
        {
            var path = GetSegmentPath(targetFile, index);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private readonly record struct RemoteFileInfo(long? Length, bool SupportsRanges);

    private sealed class RangeDownloadNotSupportedException : Exception;

    private sealed class TransferProgressReporter
    {
        private readonly object _sync = new();
        private readonly long? _totalBytes;
        private readonly Action<int> _progress;
        private readonly Action<DownloadTransferProgress> _transferProgress;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _bytesReceived;
        private long _lastReportedBytes;
        private TimeSpan _lastReportedAt;
        private double _smoothedBytesPerSecond;
        private int _lastPercent = -1;

        public TransferProgressReporter(
            long? totalBytes,
            Action<int> progress,
            Action<DownloadTransferProgress> transferProgress,
            long initialBytes = 0)
        {
            _totalBytes = totalBytes;
            _progress = progress;
            _transferProgress = transferProgress;
            _bytesReceived = initialBytes;
            _lastReportedBytes = initialBytes;
        }

        public void Report(int bytesRead)
        {
            lock (_sync)
            {
                _bytesReceived += bytesRead;
                var elapsed = _stopwatch.Elapsed;
                if (bytesRead > 0 &&
                    elapsed - _lastReportedAt < TimeSpan.FromMilliseconds(200) &&
                    _bytesReceived != _totalBytes)
                {
                    return;
                }

                var sampleSeconds = (elapsed - _lastReportedAt).TotalSeconds;
                if (sampleSeconds > 0)
                {
                    var sampleSpeed = (_bytesReceived - _lastReportedBytes) / sampleSeconds;
                    _smoothedBytesPerSecond = _smoothedBytesPerSecond <= 0
                        ? sampleSpeed
                        : (_smoothedBytesPerSecond * 0.7) + (sampleSpeed * 0.3);
                }

                _lastReportedAt = elapsed;
                _lastReportedBytes = _bytesReceived;
                var percent = _totalBytes is > 0
                    ? (int)Math.Min(99, _bytesReceived * 100 / _totalBytes.Value)
                    : 0;
                if (percent != _lastPercent)
                {
                    _progress(percent);
                    _lastPercent = percent;
                }

                _transferProgress(new DownloadTransferProgress(
                    _bytesReceived,
                    _totalBytes,
                    Math.Max(0, _smoothedBytesPerSecond)));
            }
        }

        public void Complete()
        {
            lock (_sync)
            {
                _progress(100);
                _lastPercent = 100;
                _transferProgress(new DownloadTransferProgress(
                    _bytesReceived,
                    _totalBytes,
                    Math.Max(0, _smoothedBytesPerSecond)));
            }
        }

        public void Rollback(long bytes)
        {
            if (bytes <= 0)
            {
                return;
            }

            lock (_sync)
            {
                _bytesReceived = Math.Max(0, _bytesReceived - bytes);
                _lastReportedBytes = Math.Min(_lastReportedBytes, _bytesReceived);
                _lastPercent = _totalBytes is > 0
                    ? (int)Math.Min(99, _bytesReceived * 100 / _totalBytes.Value)
                    : 0;
            }
        }
    }
}
