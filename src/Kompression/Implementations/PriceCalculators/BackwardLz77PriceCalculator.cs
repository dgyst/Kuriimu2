﻿using Kompression.Interfaces;
using Kompression.Models;

namespace Kompression.Implementations.PriceCalculators
{
    public class BackwardLz77PriceCalculator : IPriceCalculator
    {
        public int CalculateLiteralPrice(int value)
        {
            return 9;
        }

        public int CalculateMatchPrice(Match match)
        {
            return 17;
        }
    }
}
