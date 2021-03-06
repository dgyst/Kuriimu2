﻿using Kontract.Kompression;

namespace Kompression.Implementations.PriceCalculators
{
    public class BackwardLz77PriceCalculator : IPriceCalculator
    {
        public int CalculateLiteralPrice(int value, int literalRunLength, bool firstLiteralRun)
        {
            return 9;
        }

        public int CalculateMatchPrice(int displacement, int length, int matchRunLength, int firstValue)
        {
            return 17;
        }
    }
}
