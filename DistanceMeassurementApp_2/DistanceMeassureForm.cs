using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DALSA.SaperaLT.SapClassBasic;

namespace DistanceMeassurementApp2
{
    public partial class DistanceMeassureForm : Form
    {
        static private int signalLength;
        //private delegate void AccessDistanceResultControls(double distance);
        private SapAcqDevice lineaLite;
        private SapBuffer buffer;
        private SapAcqDeviceToBuf transfer;
        private SapFlatField flatField;
        private MyProcess process;
        private readonly object chartFrameLock = new object();
        private double[] pendingChartFrame;
        private double pendingAxisCoefficient;
        private string pendingChartStyle;
        private readonly Timer chartRefreshTimer;
        private string displayedChartStyle;
        
        public DistanceMeassureForm()
        {
            lineaLite = null;
            buffer = null;
            transfer = null;
            flatField = null;
            process = null;

            InitializeComponent();
            BuildStageControls();
            meassureSwitch.Text = "预览光谱";
            saveData.Visible = false;

            // Keep camera callbacks independent of chart painting. Only the newest frame
            // needs to be shown when acquisition is faster than the display.
            chartRefreshTimer = new Timer { Interval = 50 };
            chartRefreshTimer.Tick += RefreshChart;
            chartRefreshTimer.Start();


            folderBrowserDialog1.SelectedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MeassureData");
            folderPathText.Text = folderBrowserDialog1.SelectedPath;
            
            if (!CreateNewObjects())
                Close();
        }

        private bool CreateNewObjects()
        {
            const string cameraServer = "Linea_Lite_M2048-14um_1";
            if (SapManager.GetResourceCount(cameraServer, SapManager.ResourceType.AcqDevice) <= 0)
            {
                MessageBox.Show("未找到相机 " + cameraServer + "。请先在 CamExpert 中确认相机已连接。",
                    "相机未连接", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }

            // The bundled CCF is for a 4K camera; this project targets the 2K device.
            lineaLite = new SapAcqDevice(new SapLocation(cameraServer, 0));
            buffer = new SapBufferWithTrash(2, lineaLite, SapBuffer.MemoryType.ScatterGather);
            transfer = new SapAcqDeviceToBuf(lineaLite, buffer)
            {
                XferNotifyContext = this
            };
            transfer.XferNotify += new SapXferNotifyHandler(XferNotify);
            transfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
            flatField = new SapFlatField(lineaLite);
            if (!CreateObjects())
            {
                DisposeObjects();
                return false;
            }

            return true;
        }

        private bool CreateObjects()
        {
            if (lineaLite != null && !lineaLite.Initialized)
            {
                if (!lineaLite.Create())
                {
                    DestroyObjects();
                    return false;
                }
                lineaLite.SetFeatureValue("flatfieldCorrectionMode", "Active");
                lineaLite.SetFeatureValue("PixelFormat", "Mono12");
                lineaLite.SetFeatureValue("Height", 16);
                lineaLite.SetFeatureValue("AcquisitionLineRate", (double)1000);
                lineaLite.SetFeatureValue("ExposureTime", (double)50);
                lineaLite.SetFeatureValue("sensorTDIModeSelection", "TdiAvg");
            }
            if (buffer != null && !buffer.Initialized)
            {
                if (!buffer.Create())
                {
                    DestroyObjects();
                    return false;
                }
                signalLength = buffer.Width;
            }
            if (transfer != null && !transfer.Initialized)
            {
                if (!transfer.Create())
                {
                    DestroyObjects();
                    return false;
                }
                transfer.AutoEmpty = false;
            }
            if (flatField != null && !flatField.Initialized)
            {
                if (!flatField.Create())
                {
                    DestroyObjects();
                    return false;
                }
                flatField.Enable(true, true);
            }
            process = new MyProcess(buffer, flatField, SetChart1, this);
            if (process != null && !process.Initialized)
            {
                if (!process.Create())
                {
                    DestroyObjects();
                    return false;
                }
                process.AutoEmpty = true;
            }
            return true;
        }

        private void XferNotify(object sender, SapXferNotifyEventArgs argsNotify)
        {
            DistanceMeassureForm form = argsNotify.Context as DistanceMeassureForm;
            form.process.Execute();
        }

        private bool DestroyObjects()
        {
            if (process != null && process.Initialized)
            {
                for (int i = 0; i < process.address.dataBufHandle.Length; i++)
                {
                    process.address.dataBufHandle[i].Free();
                }
                process.Destroy();
            }
            if (flatField != null && flatField.Initialized)
                flatField.Destroy();
            if (transfer != null && transfer.Initialized)
                transfer.Destroy();
            if (buffer != null && buffer.Initialized)
                buffer.Destroy();
            if (lineaLite != null && lineaLite.Initialized)
            {
                lineaLite.SetFeatureValue("flatfieldCorrectionMode", "Off");
                lineaLite.Destroy();
            }


            return true;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        private void DisposeObjects()
        {
            if (process != null)
            { process.Dispose(); process = null; }
            if (flatField != null)
            { flatField.Dispose(); flatField = null; }
            if (transfer != null)
            { transfer.Dispose(); transfer = null; }
            if (buffer != null)
            { buffer.Dispose(); buffer = null; }
            if (lineaLite != null)
            { lineaLite.Dispose(); lineaLite = null; }
        }

        /// <summary>
        /// 采样间隔有效性检测
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetAcquisitionRate_Leave(object sender, EventArgs e)
        {
            if (!double.TryParse(setAcquisitionRate.Text, out double lineRate))
            {
                MessageBox.Show("请输入数字！", "消息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                lineaLite.GetFeatureValue("AcquisitionLineRate", out lineRate);
                setAcquisitionRate.Text = lineRate.ToString();
            }
            else
            {
                lineaLite.SetFeatureValue("AcquisitionLineRate", lineRate);
            }
        }

        /// <summary>
        /// 测量开关
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MeassureSwitch_Click(object sender, EventArgs e)
        {
            if (transfer == null || !transfer.Initialized)
            {
                MessageBox.Show("相机尚未初始化。", "相机", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            if (transfer.Grabbing)
            {
                StopCameraMeasurement();
            }
            else
            {
                SelectChartStyle.SelectedItem = "光谱";
                if (!transfer.Grab())
                {
                    MessageBox.Show("相机采集启动失败。", "相机", MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                    return;
                }

                meassureSwitch.Text = "停止预览";
                meassureSwitch.BackColor = SystemColors.ButtonShadow;
            }
        }

        private void StopCameraMeasurement()
        {
            if (transfer != null && transfer.Initialized && transfer.Grabbing)
                transfer.Freeze();
            meassureSwitch.Text = "预览光谱";
            meassureSwitch.BackColor = SystemColors.ButtonHighlight;
            saveData.Enabled = false;
        }

        /// <summary>
        /// 操作图表
        /// </summary>
        public void SetChart1(object sender, SapProcessingDoneEventArgs data)
        {
            List<double> chartData = data.Context as List<double>;
            if (chartData == null || IsDisposed)
                return;

            // SapProcessing invokes this when its data is ready. Copy it before the
            // next acquisition changes the shared list; the UI timer owns rendering.
            double[] snapshot = chartData.ToArray();
            double coefficient = process.axisCorfficient;
            string style = process.CurrentChartStyle;
            lock (chartFrameLock)
            {
                pendingChartFrame = snapshot;
                pendingAxisCoefficient = coefficient;
                pendingChartStyle = style;
            }
        }

        private void RefreshChart(object sender, EventArgs e)
        {
            CheckRecordingHealth();
            double[] values;
            double coefficient;
            string frameStyle;
            lock (chartFrameLock)
            {
                values = pendingChartFrame;
                coefficient = pendingAxisCoefficient;
                frameStyle = pendingChartStyle;
                pendingChartFrame = null;
            }
            if (values == null || values.Length == 0 || chart1.IsDisposed)
                return;

            string style = SelectChartStyle.Text;
            if (frameStyle != style)
                return;
            var area = chart1.ChartAreas[0];
            if (style != displayedChartStyle)
            {
                displayedChartStyle = style;
                area.AxisX.Maximum = style == "频域" ? 200 * Math.PI / 0.24725 : 1750;
                if (style == "光谱")
                {
                    // Mono12 pixels have a fixed 0..4095 range. Auto scaling changed
                    // tick labels on every frame and made the Y axis appear to flash.
                    area.AxisY.Minimum = 0;
                    area.AxisY.Maximum = 4095;
                    area.AxisY.Title = "光谱强度";
                }
                else
                {
                    area.AxisY.Minimum = double.NaN;
                    area.AxisY.Maximum = double.NaN;
                    area.AxisY.Title = style == "距离" ? "距离/μm" : "幅值";
                }
            }

            if (style != "光谱")
                KeepYAxisStable(area.AxisY, values);

            double[] positions = new double[values.Length];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = i * coefficient;
            chart1.Series[0].Points.DataBindXY(positions, values);
        }

        private static void KeepYAxisStable(System.Windows.Forms.DataVisualization.Charting.Axis axis, double[] values)
        {
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            foreach (double value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
            if (double.IsInfinity(min))
                return;

            double margin = Math.Max((max - min) * 0.1, 1);
            if (double.IsNaN(axis.Minimum) || double.IsNaN(axis.Maximum))
            {
                axis.Minimum = min - margin;
                axis.Maximum = max + margin;
                return;
            }

            // Retain the same tick marks while the trace stays comfortably inside.
            double padding = (axis.Maximum - axis.Minimum) * 0.05;
            if (min < axis.Minimum + padding || max > axis.Maximum - padding)
            {
                axis.Minimum = Math.Min(axis.Minimum, min - margin);
                axis.Maximum = Math.Max(axis.Maximum, max + margin);
            }
        }



        /// <summary>
        /// 保存数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveData_Click(object sender, EventArgs e)
        {
            process.saveData = true;
            saveData.Enabled = false;
        }

        /// <summary>
        /// 选择文件夹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectFolderPath_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                folderPathText.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (reconstructionTask != null && !reconstructionTask.IsCompleted)
            {
                e.Cancel = true;
                SetStageStatus("三维形貌正在解算和写入文件，请完成后再关闭窗口。");
                return;
            }
            chartRefreshTimer.Stop();
            chartRefreshTimer.Dispose();
            StopCameraMeasurement();
            CloseStageControls();
            DestroyObjects();
            DisposeObjects();
        }

        private void SetExposureTime_Leave(object sender, EventArgs e)
        {
            if (!double.TryParse(SetExposureTime.Text, out double exTime))
            {
                MessageBox.Show("请输入数字！", "消息", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                lineaLite.GetFeatureValue("ExposureTime", out exTime);
                setAcquisitionRate.Text = exTime.ToString();
            }
            else
            {
                lineaLite.SetFeatureValue("ExposureTime", exTime);
            }
        }

        private void SelectChartStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            lock (chartFrameLock)
                pendingChartFrame = null;
            chart1.Series[0].Points.Clear();
            displayedChartStyle = null;
        }
    }
}
