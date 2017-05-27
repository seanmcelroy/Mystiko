using System;

namespace Mystiko.Cryptography
{
    using System.Security.Cryptography;

    using JetBrains.Annotations;

    [PublicAPI]
    public static class RandomUtility
    {
        public static int GetNext([NotNull] this RandomNumberGenerator rng, int maxValue)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));
            if (maxValue < 1)
                throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "Value must be positive.");

            return GetNext(rng, 0, maxValue);
        }

        public static int GetNext([NotNull] this RandomNumberGenerator rng, int minValue, int maxValue)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));
            if (maxValue < 1)
                throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "Value must be positive.");
            if (minValue >= maxValue)
                throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "Value must be greater than the minimum value");

            var maxRange = maxValue - minValue;

            var buffer = new byte[4];
            int bits, val;

            if ((maxRange & -maxRange) == maxRange)  // is maxRange an exact power of 2
            {
                rng.GetBytes(buffer);
                bits = BitConverter.ToInt32(buffer, 0);
                return bits & (maxRange - 1) + minValue;
            }

            do
            {
                rng.GetBytes(buffer);
                bits = BitConverter.ToInt32(buffer, 0) & 0x7FFFFFFF;
                val = bits % maxRange;
            } while (bits - val + (maxRange - 1) < 0);

            return val + minValue;
        }
    }
}
