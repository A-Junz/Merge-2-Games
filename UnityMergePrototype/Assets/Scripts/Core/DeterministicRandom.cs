using System;

namespace MergePrototype.Core
{
    /// <summary>
    /// 可序列化的确定性随机数生成器（xorshift32）。
    /// 状态可存入存档，使测试与回放可重现。
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint State
        {
            get => _state;
            set => _state = value == 0 ? 0x9E3779B9u : value;
        }

        private uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>[0, maxExclusive)</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        /// <summary>[0, 1)</summary>
        public double NextDouble()
        {
            return NextUInt() / 4294967296.0;
        }
    }
}
