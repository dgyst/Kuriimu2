﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kontract.Interfaces;
using System.Security.Cryptography;

namespace Komponent.Cryptography.AES
{
    public class EcbStream : Stream, IKryptoStream
    {
        public int BlockSize => 128;

        public int BlockSizeBytes => 16;

        public List<byte[]> Keys { get; }

        public int KeySize => Keys[0]?.Length ?? 0;

        public byte[] IV => throw new NotSupportedException();

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        private long _length = 0;
        public override long Length => _length;
        private long TotalBlocks => CalculateBlockCount(Length);

        private long CalculateBlockCount(long input) => (long)Math.Ceiling((double)input / BlockSizeBytes);

        public override long Position { get => _stream.Position; set => Seek(value, SeekOrigin.Begin); }

        private byte[] _lastBlockBuffer;
        private Stream _stream;
        private ICryptoTransform _encryptor;
        private ICryptoTransform _decryptor;

        public EcbStream(Stream input, byte[] key)
        {
            _lastBlockBuffer = new byte[BlockSizeBytes];
            _stream = input;

            Keys = new List<byte[]>();
            Keys.Add(key);

            var aes = new AesManaged
            {
                Key = key,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros
            };
            _encryptor = aes.CreateEncryptor();
            _decryptor = aes.CreateDecryptor();
        }

        public override void Flush()
        {
            if (Position % BlockSizeBytes > 0)
                Position -= Position % BlockSizeBytes;
            _stream.Write(_lastBlockBuffer, 0, _lastBlockBuffer.Length);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateInput(buffer, offset, count);

            var decrypted = ReadDecrypted(count);

            long offsetIntoBlock = 0;
            if (Position % BlockSizeBytes > 0)
                offsetIntoBlock = Position % BlockSizeBytes;
            Array.Copy(decrypted.Skip((int)offsetIntoBlock).Take(count).ToArray(), 0, buffer, 0, count);
            Position += count;

            return count;
        }
        private void ValidateInput(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0)
                throw new InvalidDataException("Offset and count can't be negative.");
            if (offset + count > buffer.Length)
                throw new InvalidDataException("Buffer too short.");
            if (count > Length - Position)
                throw new InvalidDataException($"Can't read {count} bytes from position {Position} in stream with length {Length}.");
        }
        private byte[] ReadDecrypted(int count)
        {
            long offsetIntoBlock = 0;
            if (Position % BlockSizeBytes > 0)
                offsetIntoBlock = Position % BlockSizeBytes;

            var blocksToRead = CalculateBlockCount(offsetIntoBlock + count);
            var blockPaddedCount = blocksToRead * BlockSizeBytes;

            if (Length <= 0 || count <= 0)
                return new byte[blockPaddedCount];

            var originalPosition = Position;
            Position -= offsetIntoBlock;

            var minimalDecryptableSize = (int)Math.Min(CalculateBlockCount(Length) * BlockSizeBytes, (int)blockPaddedCount);
            var bytesRead = new byte[minimalDecryptableSize];
            _stream.Read(bytesRead, 0, minimalDecryptableSize);
            Position = originalPosition;

            if (CalculateBlockCount(Position + count) >= TotalBlocks)
                Array.Copy(_lastBlockBuffer, 0, bytesRead, bytesRead.Length - BlockSizeBytes, _lastBlockBuffer.Length);

            var decrypted = _decryptor.TransformFinalBlock(bytesRead, 0, minimalDecryptableSize);
            var result = new byte[blockPaddedCount];
            Array.Copy(decrypted, 0, result, 0, minimalDecryptableSize);

            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            long offsetIntoBlock = 0;
            if (Position % BlockSizeBytes > 0)
                offsetIntoBlock = Position % BlockSizeBytes;

            var blocksToWrite = CalculateBlockCount(offsetIntoBlock + count);
            var blockPaddedCount = blocksToWrite * BlockSizeBytes;

            byte[] decrypted = ReadDecrypted(count);
            Array.Copy(buffer, 0, decrypted, offsetIntoBlock, count);

            if (CalculateBlockCount(Length) < CalculateBlockCount(Position) - 1)
            {
                var betweenBlocks = CalculateBlockCount(Position) - CalculateBlockCount(Length) - 1;
                var newDecrypted = new byte[betweenBlocks * BlockSizeBytes + decrypted.Length];
                Array.Copy(decrypted, 0, newDecrypted, betweenBlocks * BlockSizeBytes, decrypted.Length);
                decrypted = newDecrypted;
            }

            var encrypted = _encryptor.TransformFinalBlock(decrypted, 0, decrypted.Length);

            if (CalculateBlockCount(Position + count) >= TotalBlocks)
                Array.Copy(encrypted.Skip(encrypted.Length - BlockSizeBytes).Take(BlockSizeBytes).ToArray(), _lastBlockBuffer, BlockSizeBytes);

            var originalPosition = Position;
            if (CalculateBlockCount(Length) < CalculateBlockCount(Position) - 1)
                Position -= (CalculateBlockCount(Position) - CalculateBlockCount(Length) - 1) * BlockSizeBytes;
            Position = Position - offsetIntoBlock;

            _stream.Write(encrypted, 0, encrypted.Length);

            _length = Math.Max(_length, Position + count);
            Position = originalPosition + count;
        }

        public byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            Read(buffer, 0, count);
            return buffer;
        }

        public void WriteBytes(byte[] input)
        {
            Write(input, 0, input.Length);
        }

        public override void Close()
        {
            Dispose();
        }

        public new void Dispose()
        {
            Flush();

            _stream.Dispose();
            _lastBlockBuffer = null;
            _decryptor = null;
            _encryptor = null;
        }
    }
}