using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistanceMeassurementApp2
{
    // Streams every processed camera frame to one CSV without blocking the camera
    // callback on disk I/O. The display may skip frames; the recorder does not.
    internal sealed class MeasurementRecorder
    {
        private const int MaxPoints = 1750;
        private readonly BlockingCollection<Frame> frames = new BlockingCollection<Frame>(512);
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Task writerTask;
        private Exception error;
        private long written;
        private long dropped;

        public MeasurementRecorder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("请先选择数据保存文件夹。");

            Directory.CreateDirectory(folder);
            FilePath = Path.Combine(folder, "Scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") +
                "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".csv");
            var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, new UTF8Encoding(true));
            writerTask = Task.Run(() => WriteFrames(writer));
        }

        public string FilePath { get; private set; }
        public Exception Error { get { return Volatile.Read(ref error); } }
        public long WrittenFrames { get { return Interlocked.Read(ref written); } }
        public long DroppedFrames { get { return Interlocked.Read(ref dropped); } }

        public void AddFrame(int scanRow, string style, double axisStep, double[] values)
        {
            if (Error != null)
                return;

            var frame = new Frame(DateTime.UtcNow, clock.Elapsed.TotalMilliseconds,
                scanRow, style, axisStep, values);
            try
            {
                if (!frames.TryAdd(frame))
                    Interlocked.Increment(ref dropped);
            }
            catch (InvalidOperationException)
            {
                // Capture finished between reading the active recorder and enqueueing.
            }
        }

        public async Task StopAsync()
        {
            if (!frames.IsAddingCompleted)
                frames.CompleteAdding();
            await writerTask.ConfigureAwait(false);
            frames.Dispose();
        }

        private void WriteFrames(StreamWriter writer)
        {
            try
            {
                using (writer)
                {
                    var header = new StringBuilder("Frame,TimeUtc,ElapsedMs,ScanRow,Mode,AxisStep,PointCount");
                    for (int i = 0; i < MaxPoints; i++)
                        header.Append(",P").Append(i);
                    writer.WriteLine(header.ToString());

                    foreach (Frame frame in frames.GetConsumingEnumerable())
                    {
                        var row = new StringBuilder(16000);
                        row.Append(written + 1).Append(',')
                            .Append(frame.TimeUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                            .Append(frame.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                            .Append(frame.ScanRow).Append(',').Append(frame.Style).Append(',')
                            .Append(frame.AxisStep.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                            .Append(frame.Values.Length);
                        for (int i = 0; i < MaxPoints; i++)
                        {
                            row.Append(',');
                            if (i < frame.Values.Length)
                                row.Append(frame.Values[i].ToString("R", CultureInfo.InvariantCulture));
                        }
                        writer.WriteLine(row.ToString());
                        Interlocked.Increment(ref written);
                        if (written % 16 == 0)
                            writer.Flush();
                    }
                    writer.Flush();
                }
            }
            catch (Exception exception)
            {
                Volatile.Write(ref error, exception);
                writer.Dispose();
            }
        }

        private sealed class Frame
        {
            public Frame(DateTime timeUtc, double elapsedMilliseconds, int scanRow, string style,
                double axisStep, double[] values)
            {
                TimeUtc = timeUtc;
                ElapsedMilliseconds = elapsedMilliseconds;
                ScanRow = scanRow;
                Style = style;
                AxisStep = axisStep;
                Values = values;
            }

            public DateTime TimeUtc { get; private set; }
            public double ElapsedMilliseconds { get; private set; }
            public int ScanRow { get; private set; }
            public string Style { get; private set; }
            public double AxisStep { get; private set; }
            public double[] Values { get; private set; }
        }
    }
}
