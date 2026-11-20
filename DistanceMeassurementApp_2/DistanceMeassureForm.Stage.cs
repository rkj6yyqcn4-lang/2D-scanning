using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DistanceMeassurementApp2
{
    public partial class DistanceMeassureForm
    {
        private readonly StageController stageController = new StageController();
        private CancellationTokenSource stageMotionCts;
        private Task stageMotionTask;
        private bool stageMotionRunning;
        private bool stageLinkedToMeasurement;
        private MeasurementRecorder activeRecorder;
        private bool recordingFailureHandled;
        private int activeScanRow = -1;
        private ScanReconstructionOptions scanOptions;
        private Task<ScanReconstructionResult> reconstructionTask;
        private GroupBox stageGroup;
        private CheckBox zigZagScan;
        private Button stageRunSwitch;
        private Label stageStatus;
        private TextBox xStartText;
        private TextBox xDirText;
        private TextBox xFreqText;
        private TextBox xDurationText;
        private TextBox yStartText;
        private TextBox yDirText;
        private TextBox yFreqText;
        private TextBox yDurationText;
        private TextBox zigZagRowsText;
        private TextBox xStepUmText;
        private TextBox yStepUmText;
        private TextBox zScaleText;
        private CheckBox calibrationConfirmed;

        private void BuildStageControls()
        {
            SuspendLayout();
            Text = "光谱干涉测距与位移台控制";
            MinimumSize = new Size(1100, 700);
            ClientSize = new Size(1324, 890);

            stageGroup = new GroupBox
            {
                Text = "位移台控制",
                Dock = DockStyle.Bottom,
                Height = 140
            };
            Controls.Add(stageGroup);

            AddStageLabel("启动(s)", 86, 22, 70);
            AddStageLabel("方向", 166, 22, 70);
            AddStageLabel("频率(Hz)", 246, 22, 80);
            AddStageLabel("持续(s)", 336, 22, 70);
            AddStageLabel("X轴", 20, 51, 45);
            AddStageLabel("Y轴", 20, 80, 45);

            xStartText = AddStageTextBox("10.0", 80, 47);
            xDirText = AddStageTextBox("+1", 160, 47);
            xFreqText = AddStageTextBox("1500", 240, 47);
            xDurationText = AddStageTextBox("8.0", 330, 47);
            yStartText = AddStageTextBox("0.0", 80, 76);
            yDirText = AddStageTextBox("+1", 160, 76);
            yFreqText = AddStageTextBox("1000", 240, 76);
            yDurationText = AddStageTextBox("10.0", 330, 76);

            stageRunSwitch = new Button
            {
                Text = "开始扫描",
                Location = new Point(445, 50),
                Size = new Size(145, 40)
            };
            stageRunSwitch.Click += StageRunSwitch_Click;
            stageGroup.Controls.Add(stageRunSwitch);
            UpdateStageRunButton();

            zigZagScan = new CheckBox
            {
                Text = "单向逐行扫描",
                Location = new Point(645, 47),
                Size = new Size(135, 24),
                Checked = true
            };
            stageGroup.Controls.Add(zigZagScan);
            zigZagScan.CheckedChanged += (sender, args) =>
                yStartText.Enabled = !zigZagScan.Checked;
            yStartText.Enabled = false; // Raster mode steps Y after each X return.
            AddStageLabel("扫描行数", 645, 82, 80);
            zigZagRowsText = AddStageTextBox("5", 730, 78);
            AddStageLabel("X步距(μm)", 810, 22, 95);
            xStepUmText = AddStageTextBox("1.0", 905, 19);
            AddStageLabel("Y步距(μm)", 810, 53, 95);
            yStepUmText = AddStageTextBox("1.0", 905, 49);
            AddStageLabel("Z倍率", 810, 82, 65);
            zScaleText = AddStageTextBox("1.0", 875, 78);
            calibrationConfirmed = new CheckBox
            {
                Text = "标定值已确认",
                Location = new Point(970, 50),
                Size = new Size(108, 25)
            };
            stageGroup.Controls.Add(calibrationConfirmed);

            stageStatus = new Label
            {
                Text = "位移台：空闲。确认方向和行程后再启动。",
                Location = new Point(20, 107),
                Size = new Size(1050, 25),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            stageGroup.Controls.Add(stageStatus);

            PerformLayout();
            chart1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            chart1.ChartAreas[0].Position = new System.Windows.Forms.DataVisualization.Charting.ElementPosition(1, 1, 98, 98);
            chart1.Size = new Size(
                Math.Max(600, ClientSize.Width - chart1.Left - 12),
                Math.Max(240, stageGroup.Top - chart1.Top - 8));
            ResumeLayout();
        }

        private void AddStageLabel(string text, int x, int y, int width)
        {
            stageGroup.Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 22)
            });
        }

        private TextBox AddStageTextBox(string text, int x, int y)
        {
            TextBox box = new TextBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(66, 25)
            };
            stageGroup.Controls.Add(box);
            return box;
        }

        private void StageRunSwitch_Click(object sender, EventArgs e)
        {
            if (stageMotionRunning)
            {
                StopStageMotion();
                StopCameraMeasurement();
            }
            else
                StartScan();
        }

        private void UpdateStageRunButton()
        {
            stageRunSwitch.Text = stageMotionRunning ? "停止扫描" : "开始扫描";
        }

        private bool StartScan()
        {
            if (transfer == null || !transfer.Initialized)
            {
                MessageBox.Show("相机尚未初始化。", "相机", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }
            if (stageMotionRunning)
            {
                MessageBox.Show("位移台正在运动，请先停止当前运动。", "位移台", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }
            if (!TryReadStageSettings(out StageMotionSettings settings))
                return false;
            if (settings.XDurationSeconds <= 0)
            {
                MessageBox.Show("扫描时 X 轴持续时间必须大于 0。", "位移台参数错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }

            try
            {
                scanOptions = new ScanReconstructionOptions
                {
                    ExpectedRows = settings.ZigZagEnabled ? settings.ZigZagRows : 1,
                    XStepUm = ParsePositiveCalibration(xStepUmText.Text, "X 步距"),
                    YStepUm = ParsePositiveCalibration(yStepUmText.Text, "Y 步距"),
                    ZScale = ParsePositiveCalibration(zScaleText.Text, "Z 倍率"),
                    CalibrationConfirmed = calibrationConfirmed.Checked
                };
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "重建参数错误", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }

            SelectChartStyle.SelectedItem = "光谱";
            Volatile.Write(ref activeScanRow, -1);

            try
            {
                activeRecorder = new MeasurementRecorder(folderPathText.Text);
            }
            catch (Exception error)
            {
                MessageBox.Show("无法创建采样文件：" + error.Message, "保存数据", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return false;
            }

            bool cameraWasGrabbing = transfer.Grabbing;
            if (!cameraWasGrabbing && !transfer.Grab())
            {
                StopRecording();
                MessageBox.Show("相机采集启动失败。", "相机", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return false;
            }
            if (!StartStageMotion(true, settings))
            {
                if (!cameraWasGrabbing)
                    StopCameraMeasurement();
                StopRecording();
                return false;
            }

            meassureSwitch.Text = "扫描中";
            meassureSwitch.Enabled = false;
            SelectChartStyle.Enabled = false;
            return true;
        }

        // Called by SapProcessing for every acquired frame, before the next frame
        // replaces its shared chartData list.
        internal void RecordMeasurementFrame(string style, double axisStep, System.Collections.Generic.List<double> values)
        {
            MeasurementRecorder recorder = Volatile.Read(ref activeRecorder);
            if (recorder != null && style == "光谱")
                recorder.AddFrame(Volatile.Read(ref activeScanRow), style, axisStep, values.ToArray());
        }

        private MeasurementRecorder StopRecording()
        {
            MeasurementRecorder recorder = Interlocked.Exchange(ref activeRecorder, null);
            if (recorder != null)
                recorder.StopAsync().GetAwaiter().GetResult();
            return recorder;
        }

        private void CheckRecordingHealth()
        {
            MeasurementRecorder recorder = Volatile.Read(ref activeRecorder);
            if (recorder == null || !stageMotionRunning || !stageLinkedToMeasurement || recordingFailureHandled ||
                (recorder.Error == null && recorder.DroppedFrames == 0))
                return;

            recordingFailureHandled = true;
            StopStageMotion();
            StopCameraMeasurement();
            SetStageStatus("采样写入异常或缓存已满，正在停止位移台。");
        }

        private bool TryReadStageSettings(out StageMotionSettings settings)
        {
            try
            {
                settings = ReadStageSettingsFromUi();
                StageController.Validate(settings);
                return true;
            }
            catch (Exception error)
            {
                settings = null;
                MessageBox.Show(error.Message, "位移台参数错误", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return false;
            }
        }

        private bool StartStageMotion(bool linkedToMeasurement, StageMotionSettings settings)
        {

            stageMotionCts = new CancellationTokenSource();
            stageLinkedToMeasurement = linkedToMeasurement;
            stageMotionRunning = true;
            recordingFailureHandled = false;
            UpdateStageRunButton();
            SetStageStatus("位移台：正在运行");

            try
            {
                stageMotionTask = stageController.RunScheduleAsync(
                    settings, SetStageStatus,
                    row => Volatile.Write(ref activeScanRow, row), stageMotionCts.Token);
            }
            catch (Exception error)
            {
                stageMotionRunning = false;
                UpdateStageRunButton();
                stageMotionCts.Dispose();
                stageMotionCts = null;
                MessageBox.Show(error.Message, "位移台错误", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return false;
            }

            stageMotionTask.ContinueWith(task =>
            {
                if (IsDisposed || Disposing || !IsHandleCreated)
                    return;
                try
                {
                    BeginInvoke(new Action(() => FinishStageMotion(task)));
                }
                catch (InvalidOperationException)
                {
                    // The form was closed while the worker was stopping.
                }
            });
            return true;
        }

        private async void FinishStageMotion(Task task)
        {
            try
            {
            bool wasLinked = stageLinkedToMeasurement;
            bool wasStopping = stageMotionCts != null && stageMotionCts.IsCancellationRequested;
            stageLinkedToMeasurement = false;
            stageMotionRunning = false;
            meassureSwitch.Enabled = true;
            SelectChartStyle.Enabled = true;
            stageRunSwitch.Enabled = false;
            UpdateStageRunButton();
            stageMotionCts?.Dispose();
            stageMotionCts = null;

            if (wasLinked)
                StopCameraMeasurement();

            MeasurementRecorder recording = wasLinked ? StopRecording() : null;
            string result = task.IsCanceled || wasStopping ? "位移台：已停止" : "位移台：运动完成";

            if (task.IsFaulted && task.Exception != null)
            {
                Exception error = task.Exception.GetBaseException();
                result = "位移台错误：" + error.Message;
                MessageBox.Show(error.Message, "位移台错误", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }

            if (recording != null)
            {
                result += string.Format("；已保存 {0} 帧：{1}",
                    recording.WrittenFrames, recording.FilePath);
                if (recording.Error != null || recording.DroppedFrames != 0)
                {
                    string detail = recording.Error != null ? recording.Error.Message :
                        "写入速度不足，丢失 " + recording.DroppedFrames + " 帧。";
                    MessageBox.Show("采样文件未完整保存：" + detail + "\n" + recording.FilePath,
                        "保存数据", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            SetStageStatus(result);

            if (recording != null && recording.Error == null && recording.DroppedFrames == 0 &&
                recording.WrittenFrames > 0 && !task.IsFaulted)
            {
                SetStageStatus("原始光谱已保存，正在自动解算三维形貌…");
                try
                {
                    ScanReconstructionOptions options = scanOptions;
                    reconstructionTask = Task.Run(() => ScanReconstruction.Run(recording.FilePath, options));
                    ScanReconstructionResult output = await reconstructionTask;
                    if (!IsDisposed && !Disposing)
                    {
                        SetStageStatus(string.Format("形貌已生成：{0} 行 × {1} 点；{2}",
                            output.Rows, output.Columns, output.HeightCsv));
                        MessageBox.Show("光谱与形貌文件已保存到所选文件夹：\n" +
                            recording.FilePath + "\n" + output.HeightCsv + "\n" +
                            output.HeightTiff + "\n" + output.HeightGsf + "\n" +
                            output.PointCsv + "\n" + output.PointPly,
                            "扫描完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception error)
                {
                    if (!IsDisposed && !Disposing)
                    {
                        SetStageStatus("原始光谱已保存；自动解算失败：" + error.Message);
                        MessageBox.Show("原始光谱已保存：\n" + recording.FilePath +
                            "\n自动解算失败：" + error.Message,
                            "扫描结果", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
                finally
                {
                    reconstructionTask = null;
                }
            }
            if (!IsDisposed && !Disposing)
                stageRunSwitch.Enabled = true;
            }
            catch (Exception error)
            {
                stageMotionRunning = false;
                stageLinkedToMeasurement = false;
                try { StopCameraMeasurement(); } catch { }
                try { StopRecording(); } catch { }
                if (!IsDisposed && !Disposing)
                {
                    meassureSwitch.Enabled = true;
                    SelectChartStyle.Enabled = true;
                    stageRunSwitch.Enabled = true;
                    UpdateStageRunButton();
                    SetStageStatus("扫描结束处理失败：" + error.Message);
                    MessageBox.Show(error.Message, "扫描结束处理失败", MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                }
            }
        }

        private void StopStageMotion()
        {
            if (!stageMotionRunning)
                return;

            stageMotionCts?.Cancel();
            SetStageStatus("位移台：正在停止");
            try
            {
                stageController.Stop();
            }
            catch (Exception error)
            {
                SetStageStatus("位移台停止失败：" + error.Message);
            }
        }

        private void CloseStageControls()
        {
            StopStageMotion();
            if (stageMotionTask != null && !stageMotionTask.IsCompleted)
            {
                try
                {
                    if (!stageMotionTask.Wait(3000))
                    {
                        stageMotionTask.ContinueWith(_ => stageController.Dispose());
                        StopRecording();
                        return;
                    }
                }
                catch (AggregateException)
                {
                    // The worker can complete as cancelled during shutdown.
                }
            }
            stageController.Dispose();
            stageMotionCts?.Dispose();
            StopRecording();
        }

        private StageMotionSettings ReadStageSettingsFromUi()
        {
            return new StageMotionSettings
            {
                XStartTimeSeconds = ParseStageDouble(xStartText.Text, "X 轴启动时间"),
                XDir = ParseStageDirection(xDirText.Text, "X 轴方向"),
                XFreqHz = ParseStageInt(xFreqText.Text, "X 轴 PWM 频率"),
                XDurationSeconds = ParseStageDouble(xDurationText.Text, "X 轴持续时间"),
                YStartTimeSeconds = ParseStageDouble(yStartText.Text, "Y 轴启动时间"),
                YDir = ParseStageDirection(yDirText.Text, "Y 轴方向"),
                YFreqHz = ParseStageInt(yFreqText.Text, "Y 轴 PWM 频率"),
                YDurationSeconds = ParseStageDouble(yDurationText.Text, "Y 轴持续时间"),
                ZigZagEnabled = zigZagScan.Checked,
                ZigZagRows = ParseStageInt(zigZagRowsText.Text, "扫描行数")
            };
        }

        private static double ParseStageDouble(string text, string name)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ||
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            throw new FormatException(name + "必须是数字。");
        }

        private static double ParsePositiveCalibration(string text, string name)
        {
            double value = ParseStageDouble(text, name);
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new FormatException(name + "必须是大于 0 的有限数字。");
            return value;
        }

        private static int ParseStageInt(string text, string name)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return value;
            throw new FormatException(name + "必须是整数。");
        }

        private static int ParseStageDirection(string text, string name)
        {
            int value = ParseStageInt(text, name);
            if (value == 1 || value == -1)
                return value;
            throw new FormatException(name + "只能填写 +1 或 -1。");
        }

        private void SetStageStatus(string message)
        {
            if (stageStatus == null || stageStatus.IsDisposed)
                return;
            if (stageStatus.InvokeRequired)
            {
                if (stageStatus.IsHandleCreated)
                {
                    try
                    {
                        stageStatus.BeginInvoke(new Action(() => SetStageStatus(message)));
                    }
                    catch (InvalidOperationException)
                    {
                        // The form is closing.
                    }
                }
            }
            else
            {
                stageStatus.Text = message;
            }
        }
    }
}
