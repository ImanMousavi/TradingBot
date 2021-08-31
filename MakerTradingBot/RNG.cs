using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MakerTradingBot
{
    public class RNG
    {
        readonly RNGCryptoServiceProvider csp;

        public RNG()
        {
            csp = new RNGCryptoServiceProvider();
        }

        public int Next(int minValue, int maxExclusiveValue)
        {
            if (minValue >= maxExclusiveValue)
                throw new ArgumentOutOfRangeException("minValue must be lower than maxExclusiveValue");

            long diff = (long)maxExclusiveValue - minValue;
            long upperBound = uint.MaxValue / diff * diff;

            uint ui;
            do
            {
                ui = GetRandomUInt();
            } while (ui >= upperBound);
            return (int)(minValue + (ui % diff));
        }

        public float Next(float min, float max)
        {
            float random = NextFloat();
            float diff = max - min;
            float r = random * diff;
            return min + r;
        }

        public double NextDouble()
        {
            var randomBytes = GenerateRandomBytes(12);
            //var raw = BitConverter.ToDouble(randomBytes, 0);
            //double normalised = (raw - double.MinValue) / (double.MaxValue - double.MinValue);
            //return normalised;

            var stringBuilder = new StringBuilder("0.");
            var numbers = randomBytes.Select(i => Convert.ToInt32((i * 100 / 255) / 10)).ToArray();

            foreach (var number in numbers)
            {
                stringBuilder.Append(number);
            }
            var randomNumber = Convert.ToDouble(stringBuilder.ToString());

            return randomNumber;
        }

        public float NextFloat()
        {
            var randomBytes = GenerateRandomBytes(12);
            //var raw = BitConverter.ToSingle(randomBytes, 0);
            //float normalised = (raw - float.MinValue) / (float.MaxValue - float.MinValue);

            //return normalised;

            var stringBuilder = new StringBuilder("0.");
            var numbers = randomBytes.Select(i => Convert.ToInt32((i * 100 / 255) / 10)).ToArray();

            foreach (var number in numbers)
            {
                stringBuilder.Append(number);
            }
            var randomNumber = Convert.ToSingle(stringBuilder.ToString());

            return randomNumber;
        }



        private uint GetRandomUInt()
        {
            var randomBytes = GenerateRandomBytes(sizeof(uint));
            return BitConverter.ToUInt32(randomBytes, 0);
        }

        private byte[] GenerateRandomBytes(int bytesNumber)
        {
            byte[] buffer = new byte[bytesNumber];
            csp.GetBytes(buffer);
            return buffer;
        }
    }
}
