using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DistanceMeassurementApp2
{
    internal sealed class StageController : IDisposable
    {
        private const string DllPath = "USBDAQ_DLL_V12X64.dll";
        private const float RunDuty = 50.0f;
        private const float StopDuty = 100.0f;

        private static readonly AxisConfig XAxis = new AxisConfig("X", 1, 2, false);
        private static readonly AxisConfig YAxis = new AxisConfig("Y", 2, 4, false);

        private readonly object syncRoot = new object();
        private bool isOpen;
        private bool stopRequested;

        public Task RunScheduleAsync(StageMotionSettings settings, Action<string> log,
            Action<int> setForwardRow, CancellationToken cancellationToken)
        {
            return Task.Run(delegate
            {
                RunSchedule(settings, log, setForwardRow, cancellationToken);
            }, cancellationToken);
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                stopRequested = true;
                if (isOpen)
                {
                    SafeStopNoThrow();
                }
            }
        }

        public void Dispose()
        {
            Close();
        }

        private void RunSchedule(StageMotionSettings settings, Action<string> log,
            Action<int> setForwardRow, CancellationToken cancellationToken)
        {
            Validate(settings);
            Open();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetPWM(XAxis.PwmChan, 100, StopDuty);
                SetPWM(YAxis.PwmChan, 100, StopDuty);
                SleepSeconds(0.2, cancellationToken);

                SetAxisDir(XAxis, settings.XDir);
                SetAxisDir(YAxis, settings.YDir);
                SleepSeconds(0.1, cancellationToken);

                if (settings.ZigZagEnabled)
                {
                    log("位移台：开始单向逐行扫描。");
                    ExecuteUnidirectionalRasterScan(settings, log, setForwardRow, cancellationToken);
                }
                else
                {
                    log("位移台：开始按时间表运动。");
                    ExecuteTimedEvents(settings, log, setForwardRow, cancellationToken);
                }

                SetPWM(XAxis.PwmChan, settings.XFreqHz, StopDuty);
                SetPWM(YAxis.PwmChan, settings.YFreqHz, StopDuty);
                log("位移台：运动完成。");
            }
            catch (OperationCanceledException)
            {
                SafeStopNoThrow();
                log("位移台：运动已停止。");
                // A user pressing Stop is normal completion, not an unhandled task
                // exception. The form uses its cancellation source to show "stopped".
            }
            finally
            {
                setForwardRow(-1);
                Close();
            }
        }

        private void Open()
        {
            lock (syncRoot)
            {
                if (isOpen)
                {
                    throw new InvalidOperationException("位移台正在运行，请先停止当前运动。");
                }

                CheckReturn(OpenUsbV12(), "OpenUsbV12 打开失败");
                isOpen = true;
                stopRequested = false;
            }
        }

        internal static void Validate(StageMotionSettings settings)
        {
            if (double.IsNaN(settings.XStartTimeSeconds) || double.IsInfinity(settings.XStartTimeSeconds) ||
                double.IsNaN(settings.YStartTimeSeconds) || double.IsInfinity(settings.YStartTimeSeconds) ||
                double.IsNaN(settings.XDurationSeconds) || double.IsInfinity(settings.XDurationSeconds) ||
                double.IsNaN(settings.YDurationSeconds) || double.IsInfinity(settings.YDurationSeconds))
            {
                throw new ArgumentOutOfRangeException("位移台时间参数必须是有限数字。");
            }

            if (settings.XFreqHz < 1 || settings.YFreqHz < 1)
            {
                throw new ArgumentOutOfRangeException("PWM 频率不能小于 1 Hz。");
            }

            if (settings.XDurationSeconds < 0 || settings.YDurationSeconds < 0)
            {
                throw new ArgumentOutOfRangeException("运动持续时间不能为负数。");
            }

            if (settings.XStartTimeSeconds < 0 || settings.YStartTimeSeconds < 0)
            {
                throw new ArgumentOutOfRangeException("启动时间不能为负数。");
            }

            if ((settings.XDir != 1 && settings.XDir != -1) || (settings.YDir != 1 && settings.YDir != -1))
            {
                throw new ArgumentOutOfRangeException("方向只能填写 +1 或 -1。");
            }

            if (settings.ZigZagEnabled)
            {
                if (settings.ZigZagRows < 1)
                {
                    throw new ArgumentOutOfRangeException("之字形行数不能小于 1。");
                }

                if (settings.XDurationSeconds <= 0)
                {
                    throw new ArgumentOutOfRangeException("之字形扫描时，X 轴持续时间必须大于 0。");
                }

                if (settings.ZigZagRows > 1 && settings.YDurationSeconds <= 0)
                {
                    throw new ArgumentOutOfRangeException("之字形扫描行数大于 1 时，Y 轴步进时间必须大于 0。");
                }
            }
        }

        private void ExecuteTimedEvents(StageMotionSettings settings, Action<string> log,
            Action<int> setForwardRow, CancellationToken cancellationToken)
        {
            List<MotionEvent> events = new List<MotionEvent>();

            if (settings.XDurationSeconds > 0)
            {
                events.Add(new MotionEvent(settings.XStartTimeSeconds, XAxis, MotionAction.Start));
                events.Add(new MotionEvent(settings.XStartTimeSeconds + settings.XDurationSeconds, XAxis, MotionAction.Stop));
            }

            if (settings.YDurationSeconds > 0)
            {
                events.Add(new MotionEvent(settings.YStartTimeSeconds, YAxis, MotionAction.Start));
                events.Add(new MotionEvent(settings.YStartTimeSeconds + settings.YDurationSeconds, YAxis, MotionAction.Stop));
            }

            events.Sort(delegate(MotionEvent left, MotionEvent right)
            {
                return left.TimeSeconds.CompareTo(right.TimeSeconds);
            });

            Stopwatch stopwatch = Stopwatch.StartNew();
            foreach (MotionEvent motionEvent in events)
            {
                WaitUntil(stopwatch, motionEvent.TimeSeconds, cancellationToken);

                int freqHz = motionEvent.Axis.Name == "X" ? settings.XFreqHz : settings.YFreqHz;
                int dir = motionEvent.Axis.Name == "X" ? settings.XDir : settings.YDir;

                if (motionEvent.Action == MotionAction.Start)
                {
                    log(string.Format("{0:F3} s：{1} 轴启动，频率 {2} Hz，方向 {3:+#;-#}",
                        stopwatch.Elapsed.TotalSeconds, motionEvent.Axis.Name, freqHz, dir));
                    SetRunPwm(motionEvent.Axis.PwmChan, freqHz, cancellationToken);
                    if (motionEvent.Axis.Name == "X")
                        setForwardRow(0);
                }
                else
                {
                    log(string.Format("{0:F3} s：{1} 轴停止", stopwatch.Elapsed.TotalSeconds, motionEvent.Axis.Name));
                    SetPWM(motionEvent.Axis.PwmChan, freqHz, StopDuty);
                    if (motionEvent.Axis.Name == "X")
                        setForwardRow(-1);
                }
            }
        }

        private void ExecuteUnidirectionalRasterScan(StageMotionSettings settings, Action<string> log,
            Action<int> setForwardRow, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            WaitUntil(stopwatch, settings.XStartTimeSeconds, cancellationToken);

            for (int row = 1; row <= settings.ZigZagRows; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SetAxisDir(XAxis, settings.XDir);
                SleepSeconds(0.05, cancellationToken);

                log(string.Format("{0:F3} s：第 {1}/{2} 行，X 轴正向扫描，方向 {3:+#;-#}",
                    stopwatch.Elapsed.TotalSeconds, row, settings.ZigZagRows, settings.XDir));
                RunAxisForSeconds(XAxis, settings.XFreqHz, settings.XDurationSeconds,
                    setForwardRow, row - 1, cancellationToken);

                SetAxisDir(XAxis, -settings.XDir);
                SleepSeconds(0.05, cancellationToken);

                log(string.Format("{0:F3} s：X 轴返回起始位置，方向 {1:+#;-#}",
                    stopwatch.Elapsed.TotalSeconds, -settings.XDir));
                RunAxisForSeconds(XAxis, settings.XFreqHz, settings.XDurationSeconds,
                    setForwardRow, -1, cancellationToken);

                if (row < settings.ZigZagRows)
                {
                    SetAxisDir(YAxis, settings.YDir);
                    SleepSeconds(0.05, cancellationToken);

                    log(string.Format("{0:F3} s：Y 轴步进到下一行，方向 {1:+#;-#}",
                        stopwatch.Elapsed.TotalSeconds, settings.YDir));
                    RunAxisForSeconds(YAxis, settings.YFreqHz, settings.YDurationSeconds,
                        setForwardRow, -1, cancellationToken);
                }
            }
        }

        private void RunAxisForSeconds(AxisConfig axis, int freqHz, double seconds,
            Action<int> setForwardRow, int rowIndex, CancellationToken cancellationToken)
        {
            SetRunPwm(axis.PwmChan, freqHz, cancellationToken);
            if (rowIndex >= 0)
                setForwardRow(rowIndex);
            try
            {
                SleepSeconds(seconds, cancellationToken);
            }
            finally
            {
                if (rowIndex >= 0)
                    setForwardRow(-1);
                SetPWM(axis.PwmChan, freqHz, StopDuty);
            }
        }

        private static void WaitUntil(Stopwatch stopwatch, double targetSeconds, CancellationToken cancellationToken)
        {
            while (stopwatch.Elapsed.TotalSeconds < targetSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(1);
            }
        }

        private static void SetAxisDir(AxisConfig axis, int dirSign)
        {
            bool dirState = dirSign > 0;
            if (axis.DirInvert)
            {
                dirState = !dirState;
            }

            WriteDO(axis.DirDO, dirState);
        }

        private void SetRunPwm(int pwmChan, int freqHz, CancellationToken cancellationToken)
        {
            lock (syncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stopRequested)
                {
                    throw new OperationCanceledException("位移台运动已停止。", cancellationToken);
                }
                SetPWM(pwmChan, freqHz, RunDuty);
            }
        }

        private static void SetPWM(int pwmChan, int freq, float duty)
        {
            int ret = PWMOutSetV12(pwmChan, freq, duty);
            if (ret != 0)
            {
                throw new InvalidOperationException(string.Format(
                    "PWMOutSetV12 设置失败：PWM{0}, freq={1}, duty={2:F1}, return={3}",
                    pwmChan, freq, duty, ret));
            }
        }

        private static void WriteDO(int doChan, bool state)
        {
            int ret = DoSetV12((byte)doChan, state ? (byte)1 : (byte)0);
            if (ret != 0)
            {
                throw new InvalidOperationException(string.Format(
                    "DoSetV12 输出失败：DO{0}, state={1}, return={2}",
                    doChan, state ? 1 : 0, ret));
            }
        }

        private void Close()
        {
            lock (syncRoot)
            {
                if (!isOpen)
                {
                    return;
                }

                SafeStopNoThrow();
                CloseUsbV12();
                isOpen = false;
            }
        }

        private static void SafeStopNoThrow()
        {
            try
            {
                PWMOutSetV12(XAxis.PwmChan, 100, StopDuty);
                PWMOutSetV12(YAxis.PwmChan, 100, StopDuty);
            }
            catch
            {
            }
        }

        private static void SleepSeconds(double seconds, CancellationToken cancellationToken)
        {
            int totalMilliseconds = Math.Max(0, (int)Math.Round(seconds * 1000.0));
            int waited = 0;
            while (waited < totalMilliseconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int step = Math.Min(20, totalMilliseconds - waited);
                Thread.Sleep(step);
                waited += step;
            }
        }

        private static void CheckReturn(int ret, string message)
        {
            if (ret != 0)
            {
                throw new InvalidOperationException(string.Format("{0}，返回值={1}", message, ret));
            }
        }

        [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
        private static extern int OpenUsbV12();

        [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
        private static extern int CloseUsbV12();

        [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
        private static extern int PWMOutSetV12(int chan, int freq, float dutyCycle);

        [DllImport(DllPath, CallingConvention = CallingConvention.StdCall)]
        private static extern int DoSetV12(byte chan, byte state);

        private sealed class AxisConfig
        {
            public AxisConfig(string name, int pwmChan, int dirDO, bool dirInvert)
            {
                Name = name;
                PwmChan = pwmChan;
                DirDO = dirDO;
                DirInvert = dirInvert;
            }

            public string Name { get; private set; }

            public int PwmChan { get; private set; }

            public int DirDO { get; private set; }

            public bool DirInvert { get; private set; }
        }

        private sealed class MotionEvent
        {
            public MotionEvent(double timeSeconds, AxisConfig axis, MotionAction action)
            {
                TimeSeconds = timeSeconds;
                Axis = axis;
                Action = action;
            }

            public double TimeSeconds { get; private set; }

            public AxisConfig Axis { get; private set; }

            public MotionAction Action { get; private set; }
        }

        private enum MotionAction
        {
            Start,
            Stop
        }
    }

    internal sealed class StageMotionSettings
    {
        public double XStartTimeSeconds { get; set; }

        public int XDir { get; set; }

        public int XFreqHz { get; set; }

        public double XDurationSeconds { get; set; }

        public double YStartTimeSeconds { get; set; }

        public int YDir { get; set; }

        public int YFreqHz { get; set; }

        public double YDurationSeconds { get; set; }

        public bool ZigZagEnabled { get; set; }

        public int ZigZagRows { get; set; }
    }
}
