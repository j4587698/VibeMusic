using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace KuGouLiteSdk.Crypto
{
    public class KugouCryptoHttpStream : Stream
    {
        private readonly string _url;
        private readonly HttpClient _httpClient;
        private readonly long _totalLength;
        private readonly byte[] _key;
        private readonly int _size;
        private readonly int _blockSize = 5120;
        private readonly int[] _offsetCache;
        private readonly byte[] _boxTemplate;
        private readonly uint _hash;

        private long _position;
        private Stream? _activeHttpResponseStream;
        private HttpResponseMessage? _activeHttpResponse;
        private CancellationTokenSource? _activeRequestCts;
        private bool _disposed;

        public KugouCryptoHttpStream(string url, string enEkey, long totalLength, HttpClient? httpClient = null)
        {
            _url = url;
            _totalLength = totalLength;
            _httpClient = httpClient ?? new HttpClient();

            _key = KugouTeaCrypto.DecryptEKey(enEkey);
            _size = _key.Length;
            
            _boxTemplate = Enumerable.Range(0, _size).Select(x => (byte)x).ToArray();

            int j = 0;
            for (int i = 0; i < _size; i++)
            {
                j = (j + _boxTemplate[i] + _key[i]) % _size;
                (_boxTemplate[i], _boxTemplate[j]) = (_boxTemplate[j], _boxTemplate[i]);
            }

            _hash = 1;
            for (int i = 0; j < _size; i++)
            {
                if (_key[i] == 0) continue;
                uint next = _hash * _key[i];
                if (next == 0 || next <= _hash) break;
                _hash = next;
            }

            _offsetCache = new int[_blockSize];
            for (int i = 0; i < _blockSize; i++)
            {
                _offsetCache[i] = GetOffset(i);
            }
        }

        private int GetCachedOffset(long index)
        {
            return index < _blockSize ? _offsetCache[index] : GetOffset(index);
        }

        private int GetOffset(long index)
        {
            long sum = (long)(_hash / (double)((index + 1) * _key[index % _size]) * 100);
            return (int)(sum % _size);
        }

        private void ProcessBlock(Span<byte> buffer, long offset)
        {
            int j = 0, k = 0;
            var box = (stackalloc byte[_size]);
            _boxTemplate.CopyTo(box);
            int skipLength = (int)(offset % _blockSize) + GetCachedOffset(offset / _blockSize);

            for (int i = -skipLength; i < buffer.Length; i++)
            {
                j = (j + 1) % _size;
                k = (box[j] + k) % _size;
                (box[j], box[k]) = (box[k], box[j]);
                if (i >= 0) buffer[i] ^= box[(box[j] + box[k]) % _size];
            }
        }

        private async Task EnsureStreamActiveAsync(CancellationToken cancellationToken)
        {
            if (_activeHttpResponseStream != null) return;
            if (_position >= _totalLength) return;

            var request = new HttpRequestMessage(HttpMethod.Get, _url);
            request.Headers.Range = new RangeHeaderValue(_position, null);
            
            _activeRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeHttpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _activeRequestCts.Token).ConfigureAwait(false);
            _activeHttpResponse.EnsureSuccessStatusCode();
            
            _activeHttpResponseStream = await _activeHttpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KugouCryptoHttpStream));
            
            if (_position >= _totalLength) return 0;

            await EnsureStreamActiveAsync(cancellationToken).ConfigureAwait(false);

            if (_activeHttpResponseStream == null) return 0;

            int read = await _activeHttpResponseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                ProcessBlock(buffer.AsSpan(offset, read), _position);
                _position += read;
            }
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KugouCryptoHttpStream));

            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _totalLength + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };

            if (newPosition < 0) newPosition = 0;
            if (newPosition > _totalLength) newPosition = _totalLength;

            if (newPosition != _position)
            {
                _position = newPosition;
                // Discard the active stream so it reconnects at the new position
                ResetActiveStream();
            }

            return _position;
        }

        private void ResetActiveStream()
        {
            _activeRequestCts?.Cancel();
            _activeRequestCts?.Dispose();
            _activeRequestCts = null;

            _activeHttpResponseStream?.Dispose();
            _activeHttpResponseStream = null;

            _activeHttpResponse?.Dispose();
            _activeHttpResponse = null;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _totalLength;

        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() { }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ResetActiveStream();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
