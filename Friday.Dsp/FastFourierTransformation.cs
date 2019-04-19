using System;
using Friday.Visualization.DSP;

namespace Friday.Dsp
{
    /// <summary>
    /// Provide an Fast Fourier Transform implementation.
    /// <para>Including a few utils method which are commonly used in combination with FFT</para>
    /// </summary>
    public static class FastFourierTransformation
    {
        /// <summary>
        /// Compute an Fast Fourier Trasform.
        /// </summary>
        /// <param name="data">
        /// Array of complex numbers, this array provides the input data and is used to store the
        /// result of the FFT.
        /// </param>
        /// <param name="exponent">The exponent n.</param>
        /// <param name="mode">The <see cref="FftMode"/> to use, Use <see cref="FftMode.Forward"/> as the default value.</param>
        public static void Fft(Complex[] data, int exponent, FftMode mode = FftMode.Forward)
        {
            // count; if exponent = 12 -> c = 2^12 = 4096.
            var c = (int)Math.Pow(2, exponent);

            // Binary inversion.
            Inverse(data, c);

            int j0, j1, j2 = 1;
            float n0, n1, tr, ti, m;
            float v0 = -1, v1 = 0;

            // Move to outer scope to optimize performance.
            int j, i;

            for (var l = 0; l < exponent; l++)
            {
                n0 = 1;
                n1 = 0;
                j1 = j2;
                j2 <<= 1; //j2 * 2

                for (j = 0; j < j1; j++)
                {
                    for (i = j; i < c; i += j2)
                    {
                        j0 = i + j1;
                        //--
                        tr = n0*data[j0].Real - n1*data[j0].Imaginary;
                        ti = n0*data[j0].Imaginary + n1*data[j0].Real;
                        //--
                        data[j0].Real = data[i].Real - tr;
                        data[j0].Imaginary = data[i].Imaginary - ti;
                        //add
                        data[i].Real += tr;
                        data[i].Imaginary += ti;
                    }

                    //calc coeff
                    m = v0*n0 - v1*n1;
                    n1 = v1*n0 + v0*n1;
                    n0 = m;
                }

                if (mode == FftMode.Forward)
                {
                    v1 = (float) Math.Sqrt((1f - v0)/2f);
                }
                else
                {
                    v1 = (float) -Math.Sqrt((1f - v0)/2f);
                }
                v0 = (float) Math.Sqrt((1f + v0)/2f);
            }
            if (mode == FftMode.Forward)
            {
                Forward(data, c);
            }
        }

        private static void Forward(Complex[] data, int count)
        {
            var length = count;
            for (var i = 0; i < length; i++)
            {
                data[i].Real /= length;
                data[i].Imaginary /= length;
            }
        }


        private static void Inverse(Complex[] data, int c)
        {
            var z = 0;
            var n1 = c >> 1; // c / 2
            for (var n0 = 0; n0 < c - 1; n0++)
            {
                if (n0 < z)
                {
                    Swap(data, n0, z);
                }
                var l = n1;
                while (l <= z)
                {
                    z = z - 1;
                    l >>= 1;
                }
                z += 1;
            }
        }


        private static void Swap(Complex[] data, int index, int index2)
        {
            var tmp = data[index];
            data[index] = data[index2];
            data[index2] = tmp;
        }
    }
}
