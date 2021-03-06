﻿using System;
using System.IO;
using Kompression.Implementations.Encoders.Headerless;
using Kontract.Kompression;
using Kontract.Kompression.Configuration;
using Kontract.Models.IO;

namespace Kompression.Implementations.Encoders.Nintendo
{
    public class HuffmanEncoder : IEncoder
    {
        private readonly int _bitDepth;

        private readonly HuffmanHeaderlessEncoder _encoder;

        public HuffmanEncoder(int bitDepth, IHuffmanTreeBuilder treeBuilder, NibbleOrder nibbleOrder = NibbleOrder.LowNibbleFirst)
        {
            _bitDepth = bitDepth;
            _encoder = new HuffmanHeaderlessEncoder(bitDepth, nibbleOrder, treeBuilder);
        }

        public void Encode(Stream input, Stream output)
        {
            if (input.Length > 0xFFFFFF)
                throw new InvalidOperationException("Data to compress is too long.");

            var compressionHeader = new[] { (byte)(0x20 + _bitDepth), (byte)input.Length, (byte)((input.Length >> 8) & 0xFF), (byte)((input.Length >> 16) & 0xFF) };
            output.Write(compressionHeader, 0, 4);

            _encoder.Encode(input, output);
        }

        public void Dispose()
        {
            // nothing to dispose
        }
    }
}
