using DALSA.SaperaLT.SapClassBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static DALSA.SaperaLT.SapClassBasic.SapFeature;

namespace DistanceMeassurementApp2
{
    internal class MyProcess : SapProcessing
    {
        readonly SapFlatField field;
        public readonly List<double> chartData;
        readonly DistanceMeassureForm meassureForm;
        public bool saveData = false;
        int saveDataCount = 1;
        private readonly CalculateDistance calculateDistance;
        public Address address;
        private string chartStyleText = string.Empty;
        private string lastProcessedStyle = string.Empty;
        public string CurrentChartStyle { get { return chartStyleText; } }
        public double axisCorfficient = 1;

        public struct Address
        {
            public int counts;
            public ushort[][] signal;
            public GCHandle[] dataBufHandle;
            public IntPtr[] signalAddress;

            public Address(SapBuffer buffer, int length)
            {
                counts = buffer.Height;
                signal = new ushort[counts][];
                dataBufHandle = new GCHandle[counts];
                signalAddress = new IntPtr[counts];
                for (int i = 0; i < counts; i++)
                {
                    signal[i] = new ushort[length];
                    dataBufHandle[i] = GCHandle.Alloc(signal[i], GCHandleType.Pinned);
                    signalAddress[i] = dataBufHandle[i].AddrOfPinnedObject();
                }
            }
        }

        public MyProcess(SapBuffer buffer, SapFlatField flatField, SapProcessingDoneHandler SetChart1, Object context) : base(buffer)
        {
            field = flatField;
            calculateDistance = new CalculateDistance(1750);
            chartData = new List<double>





            {
                Capacity = 2048
            };
            meassureForm = context as DistanceMeassureForm;
            address = new Address(buffer, 1750);
            ProcessingDoneEnable = true;
            ProcessingDone += SetChart1;

            ProcessingDoneContext = chartData;
        }

        public override bool Run()
        {
            if (field.Enabled && field.SoftwareCorrection)
            {
                field.Execute(Buffer, Index);
            }


            if (meassureForm.SelectChartStyle.InvokeRequired)
            {
                meassureForm.SelectChartStyle.Invoke(new Action(() =>
                {
                    chartStyleText = meassureForm.SelectChartStyle.Text;
                }));
            }
            else
            {
                chartStyleText = meassureForm.SelectChartStyle.Text;
            }

            // The processing thread owns chartData. Clearing it from a combo-box
            // event could race with acquisition and the completed-frame callback.
            if (chartStyleText != lastProcessedStyle)
            {
                chartData.Clear();
                lastProcessedStyle = chartStyleText;
            }

            if (chartStyleText == "光谱")
            {
                axisCorfficient = 1;
                Buffer.ReadLine(Index, 1866, 1, 117, 1, address.signalAddress[0], out int numRead);
                double[] spectrum = Array.ConvertAll<ushort, double>(address.signal[0], s => s);
                chartData.Clear();
                chartData.AddRange(spectrum);
            }
            else if (chartStyleText == "频域")
            {
                axisCorfficient = Math.PI / 0.24725;
                Buffer.ReadLine(Index, 1866, 1, 117, 1, address.signalAddress[0], out int numRead);
                double[] doubles = Array.ConvertAll<ushort, double>(address.signal[0], s => s);
                chartData.Clear();
                chartData.AddRange(calculateDistance.Calculate(doubles, true));
            }
            else if (chartStyleText == "距离")
            {
                axisCorfficient = 1;
                double[] sBuffer = new double[16];
                Parallel.For(0, address.counts, (i) =>
                {
                    Buffer.ReadLine(Index, 1866, i, 117, i, address.signalAddress[i], out int numRead);
                    double[] spectrum = Array.ConvertAll<ushort, double>(address.signal[i], s => s);
                    sBuffer[i] = calculateDistance.Calculate(spectrum);
                });
                //Buffer.ReadLine(Index, 1866, 1, 117, 1, address.signalAddress[0], out int numRead);
                //double[] spectrum = Array.ConvertAll<ushort, double>(address.signal[0], s => s);
                //sBuffer[0] = calculateDistance.Calculate(spectrum);
                if (meassureForm.JumpErrorCalibration.CheckState == System.Windows.Forms.CheckState.Checked)
                {
                    if (chartData.Count > 0)
                    {
                        sBuffer[0] = sBuffer[0] + Math.Round((chartData[chartData.Count - 1] - sBuffer[0]) / calculateDistance.delta) * calculateDistance.delta;
                    }
                    if (sBuffer.Length > 1)
                    {
                        for (int i = 1; i < address.counts; i++)
                        {
                            sBuffer[i] = sBuffer[i] + Math.Round((sBuffer[i - 1] - sBuffer[i]) / calculateDistance.delta) * calculateDistance.delta;
                        }
                    }
                }
                chartData.AddRange(sBuffer);

                if (chartData.Count > 1750)
                {
                    chartData.RemoveRange(0, chartData.Count - 1750);
                }
            }


            meassureForm.RecordMeasurementFrame(chartStyleText, axisCorfficient, chartData);

            if (saveData)
            {
                saveDataCount--;
                SaveChartData();
                if (saveDataCount <= 0)
                {
                    saveData = false;
                    saveDataCount = 1;
                    meassureForm.saveData.Enabled = true;
                }
            }
            return true;
        }



        private void SaveChartData()
        {
            string filepath = meassureForm.folderPathText.Text + @"\" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "(" + (100 - saveDataCount) + ")" + ".txt";
            DirectoryInfo directoryInfo = new DirectoryInfo(meassureForm.folderPathText.Text);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }
            if (!File.Exists(filepath))
            {
                File.Create(filepath).Close();
            }
            StreamWriter sw = new StreamWriter(filepath);
            foreach (double data in chartData)
            {
                sw.WriteLine(data);
            }
            sw.Flush();
            sw.Close();
        }
    }
}
