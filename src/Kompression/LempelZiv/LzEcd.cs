﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kompression.LempelZiv.Decoders;
using Kompression.LempelZiv.Encoders;
using Kompression.LempelZiv.MatchFinder;
using Kompression.LempelZiv.Parser;

namespace Kompression.LempelZiv
{
    public class LzEcd : BaseLz
    {
        protected override int PreBufferLength => 0x3BE;

        protected override ILzEncoder CreateEncoder()
        {
            return new LzEcdEncoder();
        }

        protected override ILzParser CreateParser(int inputLength)
        {
            return new PlusOneGreedyParser(new NeedleHaystackMatchFinder(3, 0x42, 0x400, 1));
        }

        protected override ILzDecoder CreateDecoder()
        {
            return new LzEcdDecoder();
        }
    }
}