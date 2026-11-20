using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using MathNet.Numerics;
using System.Collections.Generic;

namespace DistanceMeassurementApp2
{
    public class CalculateDistance
    {
        private readonly int length = 0;
        private readonly double[] xArray;
        private readonly double[] wavenumber, background, envelop;
        private readonly double[] sampledWavenumber;
        private readonly double[] window;
        private readonly SavitzkyGolayFilter filter;
        public double delta = 0;

        public CalculateDistance(int signalLength)
        {
            length = signalLength;
            xArray = new double[length];
            wavenumber = new double[length];
            background = new double[length];
            envelop = new double[length];
            sampledWavenumber = new double[length];
            window = new double[length];
            filter = new SavitzkyGolayFilter(11, 3);
            string dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            string wavenumberPath = Path.Combine(dataDirectory, "wn.txt");
            string backgroundPath = Path.Combine(dataDirectory, "bk.txt");
            string envelopePath = Path.Combine(dataDirectory, "so.txt");
            if (File.Exists(wavenumberPath) && File.Exists(backgroundPath) && File.Exists(envelopePath))
            {
                StreamReader sr1 = new StreamReader(wavenumberPath);
                StreamReader sr2 = new StreamReader(backgroundPath);
                StreamReader sr3 = new StreamReader(envelopePath);
                for (int i = 0; i < signalLength; i++)
                {
                    xArray[i] = i;
                    if (!(double.TryParse(sr1.ReadLine(), out wavenumber[i]) && double.TryParse(sr2.ReadLine(), out background[i]) && double.TryParse(sr3.ReadLine(), out envelop[i])))
                    {
                        MessageBox.Show("数据加载失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    }
                }
                sr1.Close();
                sr2.Close();
                sr3.Close();
            }
            else
            {
                MessageBox.Show("未找到数据文件", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            double sampleInterval = (wavenumber[signalLength - 1] - wavenumber[0]) / (signalLength - 1);

            for (int i = 0; i < signalLength; i++)
            {
                sampledWavenumber[i] = wavenumber[0] + i * sampleInterval;
            }
            sampledWavenumber[signalLength - 1] = wavenumber[signalLength - 1];

            int center = length >> 1, beginIndex = length >> 2;

            double[] validWavelength = new double[center];
            for (int i = 0; i < center; i++)
            {
                validWavelength[i] = Math.PI / sampledWavenumber[i + beginIndex];
            }

            delta = validWavelength.Average();
            Array.Copy(Window.Blackman(signalLength + 1), window, signalLength);
        }
        public double Calculate(double[] spectrum)
        {

            //double[] spectrum1 = spectrum.Zip(background, (x, y) => x - y).ToArray();
            double[] spectrum1 = filter.Process(spectrum);
            //spectrum1 = spectrum1.Zip(background, (x, y) => x - y).ToArray();
            spectrum1 = spectrum1.Zip(envelop, (x, y) => x / y).ToArray();

            double[] coefficients = Fit.Polynomial(xArray, spectrum1, 5);

            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < coefficients.Length; j++)
                {
                    spectrum1[i] = spectrum1[i] - coefficients[j] * Math.Pow(i, j);
                }
            }
            //double[] spectrum3 = filter.Process(spectrum2);
            PointStruct[] points = new PointStruct[length];
            for (int i = 0; i < length; i++)
            {
                points[i] = new PointStruct(wavenumber[i], spectrum1[i]);
            }
            spectrum1 = SplineMathPlus.SplineInsertPoint(points, sampledWavenumber);
            //spectrum1 = spectrum1.Zip(window, (x, y) => x * y).ToArray();
            Complex[] spectrumfft = new Complex[length];
            for (int i = 0; i < length; i++)
            {
                spectrumfft[i] = spectrum1[i];
            }
            Fourier.Forward(spectrumfft);
            //for (int i = 11; i < length; i++) spectrumfft[i] = 0;
            int maximumIndex = 0;
            int center = length >> 1;
            double[] magnitude = new double[center + 1];
            for (int i = 0; i <= center; i++)
            {
                magnitude[i] = spectrumfft[i].Magnitude;

            }
            for (int i = 0; i < center; i++)
            {
                if (magnitude[i] > magnitude[maximumIndex]) maximumIndex = i;
            }
            int leftIndex = 0, rightIndex = 0;
            if (maximumIndex > 0)
            {
                for (leftIndex = maximumIndex - 1; (leftIndex >= 0) && (magnitude[leftIndex] < magnitude[leftIndex + 1]); leftIndex--) ;
            }
            for (rightIndex = maximumIndex + 1; (rightIndex <= center) && (magnitude[rightIndex] < magnitude[rightIndex - 1]); rightIndex++) ;
            for (int i = 0; i <= leftIndex; i++) spectrumfft[i] = 0;
            for (int i = rightIndex; i < length; i++) spectrumfft[i] = 0;
            Fourier.Inverse(spectrumfft);
            PointStruct[] phasePoints = new PointStruct[center];
            int beginIndex = length >> 2;
            int m = 0;

            double[] wrappedPhase = new double[center];
            //Complex[] retrievedSpectrum = new Complex[center];
            for (int i = 0; i < center; i++)
            {
                int j = i + beginIndex;
                //retrievedSpectrum[i] = spectrumfft[j];
                phasePoints[i].x = sampledWavenumber[j];
                wrappedPhase[i] = spectrumfft[j].Phase;
                phasePoints[i].y = wrappedPhase[i] + 2 * m * Math.PI;
                if (spectrumfft[j].Phase - spectrumfft[j + 1].Phase > Math.PI)
                {
                    m++;
                }
            }
            PointStruct line = SplineMathPlus.LinearRegression(phasePoints);
            //方法1：有效点阶次法
            //List<double> distances = new List<double>();
            //for (int i = 0; i < center; i++)
            //{
            //    if ((wrappedPhase[i] < Math.PI / 2) && (wrappedPhase[i] > - Math.PI / 2))
            //    {
            //        distances.Add((wrappedPhase[i] + 2 * Math.PI * Math.Floor((phasePoints[i].x * line.x + Math.PI) / 2 / Math.PI)) / phasePoints[i].x);
            //    }
            //}
            //double distance = distances.Average() / 2;
            //方法2：全局阶次法
            double order = GetMediumNumber(phasePoints.Zip(phasePoints, (a, b) => Math.Round((line.x * a.x - b.y) / 2 / Math.PI)).ToArray());
            double distance = phasePoints.Zip(phasePoints, (a, b) => (b.y + 2 * order * Math.PI) / 2 / a.x).Average();
            //方法3：斜率直接法           
            //double distance = line.x / 2;
            //方法4.：拟合法
            //double d = (line.x / 2) - 0.25, scand;
            //double[] amplitude = new double[500];
            //for (int i = 0; i < 500; i++)
            //{
            //    scand = d + i * 0.001;
            //    Complex[] theSpectrum = phasePoints.Zip(phasePoints, (a, b) => new Complex(Math.Cos(2 * a.x * scand), Math.Sin(-2 * a.x * scand))).ToArray();
            //    amplitude[i] = retrievedSpectrum.Zip(the0Spectrum, (a, b) => Complex.Multiply(a, b).Real).Sum();
            //}
            //var v = amplitude.Select((ma, index) => new { index, ma }).OrderByDescending(n => n.ma).Take(1);

            //double distance = d + v.First().index * 0.001;

            return distance;
        }

        public double[] Calculate(double[] spectrum, bool frequency)
        {
            if (!frequency) return null;
            double[] spectrum1 = filter.Process(spectrum);
            //spectrum1 = spectrum1.Zip(background, (x, y) => x - y).ToArray();
            spectrum1 = spectrum1.Zip(envelop, (x, y) => x / y).ToArray();

            double[] coefficients = Fit.Polynomial(xArray, spectrum1, 5);

            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < coefficients.Length; j++)
                {
                    spectrum1[i] = spectrum1[i] - coefficients[j] * Math.Pow(i, j);
                }
            }
            PointStruct[] points = new PointStruct[length];
            for (int i = 0; i < length; i++)
            {
                points[i] = new PointStruct(wavenumber[i], spectrum1[i]);
            }
            spectrum1 = SplineMathPlus.SplineInsertPoint(points, sampledWavenumber);
            //spectrum1 = spectrum1.Zip(window, (x, y) => x * y).ToArray();
            Complex[] spectrumfft = new Complex[length];
            for (int i = 0; i < length; i++)
            {
                spectrumfft[i] = spectrum1[i];
            }
            Fourier.Forward(spectrumfft);
            //int maximumIndex = 0;
            //int center = length >> 1;
            //double[] magnitude = new double[center + 1];
            //for (int i = 0; i <= center; i++)
            //{
            //    magnitude[i] = spectrumfft[i].Magnitude;

            //}
            //for (int i = 0; i < 3; i++)
            //{
            //    if (magnitude[i] > magnitude[maximumIndex]) maximumIndex = i;
            //}
            //int leftIndex = 0, rightIndex = 0;
            //if (maximumIndex > 0)
            //{
            //    for (leftIndex = maximumIndex - 1; (leftIndex >= 0) && (magnitude[leftIndex] < magnitude[leftIndex + 1]); leftIndex--) ;
            //}
            //for (rightIndex = maximumIndex + 1; (rightIndex <= center) && (magnitude[rightIndex] < magnitude[rightIndex - 1]); rightIndex++) ;
            //for (int i = 0; i <= leftIndex; i++) spectrumfft[i] = 0;
            //for (int i = rightIndex; i < length; i++) spectrumfft[i] = 0;
            return spectrumfft.Select(c => c.Magnitude).ToArray();
        }

        private double GetMediumNumber(double[] values)
        {
            Array.Sort(values);
            int middleIndex = values.Length / 2;
            if (values.Length % 2 == 0)
            {
                return (values[middleIndex - 1] + values[middleIndex]) / 2;
            }
            else
            {
                return values[middleIndex];
            }
        }

    }
}
