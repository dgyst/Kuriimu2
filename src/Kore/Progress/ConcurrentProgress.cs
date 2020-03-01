﻿using System;
using Kontract;
using Kontract.Interfaces.Progress;

namespace Kore.Progress
{
    public class ConcurrentProgress : ISetMaxProgressContext
    {
        private readonly IProgressOutput _output;
        private ProgressState _state;

        public string PreText { get; }
        public double MinPercentage { get; }
        public double MaxPercentage { get; } = 100.0;
        public long MaxValue { get; private set; } = -1;

        public ConcurrentProgress(IProgressOutput output)
        {
            ContractAssertions.IsNotNull(output, nameof(output));

            _output = output;
            _state = new ProgressState();
        }

        public ConcurrentProgress(double min, double max, IProgressOutput output) :
            this(output)
        {
            if (min > max)
                throw new InvalidOperationException($"The min value ({min}) has to be smaller than the max value ({max}).");

            MinPercentage = Math.Max(0, min);
            MaxPercentage = Math.Min(100.0, max);
        }

        public ConcurrentProgress(string preText, double min, double max, IProgressOutput output) :
            this(min, max, output)
        {
            PreText = preText;
        }

        public IProgressContext CreateScope(double min, double max) =>
            CreateScope(null, min, max);

        public IProgressContext CreateScope(string preText, double min, double max)
        {
            if (min < MinPercentage)
                throw new ArgumentOutOfRangeException(nameof(min));
            if (max > MaxPercentage)
                throw new ArgumentOutOfRangeException(nameof(max));

            return new ConcurrentProgress(preText, min, max, _output);
        }

        public ISetMaxProgressContext SetMaxValue(long maxValue)
        {
            MaxValue = maxValue;
            return this;
        }

        public void ReportProgress(string message, long partialValue, long maxValue)
        {
            _state.MinPercentage = MinPercentage;
            _state.MaxPercentage = MaxPercentage;
            _state.PartialValue = partialValue;
            _state.MaxValue = maxValue;
            _state.PreText = PreText;
            _state.Message = message;

            _output.SetProgress(_state);
        }

        public void ReportProgress(string message, long partialValue)
        {
            _state.MinPercentage = MinPercentage;
            _state.MaxPercentage = MaxPercentage;
            _state.PartialValue = partialValue;
            _state.MaxValue = MaxValue;
            _state.PreText = PreText;
            _state.Message = message;

            _output.SetProgress(_state);
        }
    }
}
