namespace DistanceMeassurementApp2
{
    partial class DistanceMeassureForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.DataPoint dataPoint1 = new System.Windows.Forms.DataVisualization.Charting.DataPoint(250D, 100D);
            this.sampleIntervalLabel = new System.Windows.Forms.Label();
            this.setAcquisitionRate = new System.Windows.Forms.ComboBox();
            this.meassureSwitch = new System.Windows.Forms.Button();
            this.distanceResult = new System.Windows.Forms.Label();
            this.chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.saveData = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.selectFolderPath = new System.Windows.Forms.Button();
            this.folderPathText = new System.Windows.Forms.Label();
            this.SelectChartStyle = new System.Windows.Forms.ComboBox();
            this.ChartLabel = new System.Windows.Forms.Label();
            this.SetExposureTime = new System.Windows.Forms.ComboBox();
            this.ExposureTimeLabel = new System.Windows.Forms.Label();
            this.JumpErrorCalibration = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).BeginInit();
            this.SuspendLayout();
            // 
            // sampleIntervalLabel
            // 
            this.sampleIntervalLabel.AutoSize = true;
            this.sampleIntervalLabel.Location = new System.Drawing.Point(19, 15);
            this.sampleIntervalLabel.Name = "sampleIntervalLabel";
            this.sampleIntervalLabel.Size = new System.Drawing.Size(99, 15);
            this.sampleIntervalLabel.TabIndex = 5;
            this.sampleIntervalLabel.Text = "采样频率(Hz)";
            // 
            // setAcquisitionRate
            // 
            this.setAcquisitionRate.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.setAcquisitionRate.FormattingEnabled = true;
            this.setAcquisitionRate.Items.AddRange(new object[] {
            "100",
            "200",
            "300",
            "500",
            "800",
            "1000",
            "1500",
            "2000",
            "2500",
            "3000"});
            this.setAcquisitionRate.Location = new System.Drawing.Point(125, 11);
            this.setAcquisitionRate.Name = "setAcquisitionRate";
            this.setAcquisitionRate.Size = new System.Drawing.Size(67, 23);
            this.setAcquisitionRate.TabIndex = 6;
            this.setAcquisitionRate.Text = "1000";
            this.setAcquisitionRate.SelectedIndexChanged += new System.EventHandler(this.SetAcquisitionRate_Leave);
            this.setAcquisitionRate.Leave += new System.EventHandler(this.SetAcquisitionRate_Leave);
            // 
            // meassureSwitch
            // 
            this.meassureSwitch.Location = new System.Drawing.Point(681, 10);
            this.meassureSwitch.Name = "meassureSwitch";
            this.meassureSwitch.Size = new System.Drawing.Size(84, 25);
            this.meassureSwitch.TabIndex = 7;
            this.meassureSwitch.Text = "预览光谱";
            this.meassureSwitch.UseVisualStyleBackColor = true;
            this.meassureSwitch.Click += new System.EventHandler(this.MeassureSwitch_Click);
            // 
            // distanceResult
            // 
            this.distanceResult.AutoSize = true;
            this.distanceResult.Location = new System.Drawing.Point(648, 45);
            this.distanceResult.Name = "distanceResult";
            this.distanceResult.Size = new System.Drawing.Size(76, 15);
            this.distanceResult.TabIndex = 8;
            this.distanceResult.Text = "距离：N/A";
            // 
            // chart1
            // 
            this.chart1.BackColor = System.Drawing.Color.Transparent;
            chartArea1.AxisX.Interval = 200D;
            chartArea1.AxisX.Maximum = 1600D;
            chartArea1.AxisX.Minimum = 0D;
            chartArea1.AxisX.Title = "扫描点数";
            chartArea1.AxisY.IsStartedFromZero = false;
            chartArea1.AxisY.Title = "距离/μm";
            chartArea1.BackColor = System.Drawing.Color.Transparent;
            chartArea1.Name = "ChartArea1";
            this.chart1.ChartAreas.Add(chartArea1);
            this.chart1.Location = new System.Drawing.Point(12, 63);
            this.chart1.Name = "chart1";
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.FastLine;
            series1.Name = "Results";
            series1.Points.Add(dataPoint1);
            this.chart1.Series.Add(series1);
            this.chart1.Size = new System.Drawing.Size(1300, 748);
            this.chart1.TabIndex = 9;
            // 
            // saveData
            // 
            this.saveData.Enabled = false;
            this.saveData.Visible = false;
            this.saveData.Location = new System.Drawing.Point(1217, 10);
            this.saveData.Name = "saveData";
            this.saveData.Size = new System.Drawing.Size(84, 25);
            this.saveData.TabIndex = 10;
            this.saveData.Text = "保存数据";
            this.saveData.UseVisualStyleBackColor = true;
            this.saveData.Click += new System.EventHandler(this.SaveData_Click);
            // 
            // folderBrowserDialog1
            // 
            this.folderBrowserDialog1.Description = "选择数据保存位置";
            this.folderBrowserDialog1.SelectedPath = "D:\\download\\MeassureData";
            // 
            // selectFolderPath
            // 
            this.selectFolderPath.Location = new System.Drawing.Point(1087, 10);
            this.selectFolderPath.Name = "selectFolderPath";
            this.selectFolderPath.Size = new System.Drawing.Size(100, 25);
            this.selectFolderPath.TabIndex = 12;
            this.selectFolderPath.Text = "选择文件夹";
            this.selectFolderPath.UseVisualStyleBackColor = true;
            this.selectFolderPath.Click += new System.EventHandler(this.SelectFolderPath_Click);
            // 
            // folderPathText
            // 
            this.folderPathText.Location = new System.Drawing.Point(794, 15);
            this.folderPathText.Name = "folderPathText";
            this.folderPathText.Size = new System.Drawing.Size(265, 15);
            this.folderPathText.TabIndex = 13;
            this.folderPathText.Text = "**folder path";
            // 
            // SelectChartStyle
            // 
            this.SelectChartStyle.FormattingEnabled = true;
            this.SelectChartStyle.Items.AddRange(new object[] {
            "光谱",
            "频域",
            "距离"});
            this.SelectChartStyle.Location = new System.Drawing.Point(468, 11);
            this.SelectChartStyle.Name = "SelectChartStyle";
            this.SelectChartStyle.Size = new System.Drawing.Size(67, 23);
            this.SelectChartStyle.TabIndex = 14;
            this.SelectChartStyle.Text = "光谱";
            this.SelectChartStyle.SelectedIndexChanged += new System.EventHandler(this.SelectChartStyle_SelectedIndexChanged);
            // 
            // ChartLabel
            // 
            this.ChartLabel.AutoSize = true;
            this.ChartLabel.Location = new System.Drawing.Point(407, 15);
            this.ChartLabel.Name = "ChartLabel";
            this.ChartLabel.Size = new System.Drawing.Size(37, 15);
            this.ChartLabel.TabIndex = 15;
            this.ChartLabel.Text = "图表";
            // 
            // SetExposureTime
            // 
            this.SetExposureTime.FormattingEnabled = true;
            this.SetExposureTime.Items.AddRange(new object[] {
            "5",
            "10",
            "20",
            "30",
            "50",
            "100",
            "200",
            "300",
            "500",
            "1000",
            "2000"});
            this.SetExposureTime.Location = new System.Drawing.Point(310, 11);
            this.SetExposureTime.Name = "SetExposureTime";
            this.SetExposureTime.Size = new System.Drawing.Size(67, 23);
            this.SetExposureTime.TabIndex = 16;
            this.SetExposureTime.Text = "50";
            this.SetExposureTime.SelectedIndexChanged += new System.EventHandler(this.SetExposureTime_Leave);
            this.SetExposureTime.Leave += new System.EventHandler(this.SetExposureTime_Leave);
            // 
            // ExposureTimeLabel
            // 
            this.ExposureTimeLabel.AutoSize = true;
            this.ExposureTimeLabel.Location = new System.Drawing.Point(227, 15);
            this.ExposureTimeLabel.Name = "ExposureTimeLabel";
            this.ExposureTimeLabel.Size = new System.Drawing.Size(67, 15);
            this.ExposureTimeLabel.TabIndex = 17;
            this.ExposureTimeLabel.Text = "曝光时间";
            // 
            // JumpErrorCalibration
            // 
            this.JumpErrorCalibration.AutoSize = true;
            this.JumpErrorCalibration.Location = new System.Drawing.Point(564, 15);
            this.JumpErrorCalibration.Name = "JumpErrorCalibration";
            this.JumpErrorCalibration.Size = new System.Drawing.Size(89, 19);
            this.JumpErrorCalibration.TabIndex = 18;
            this.JumpErrorCalibration.Text = "跳变校正";
            this.JumpErrorCalibration.UseVisualStyleBackColor = true;
            // 
            // DistanceMeassureForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1324, 823);
            this.Controls.Add(this.JumpErrorCalibration);
            this.Controls.Add(this.ExposureTimeLabel);
            this.Controls.Add(this.SetExposureTime);
            this.Controls.Add(this.ChartLabel);
            this.Controls.Add(this.SelectChartStyle);
            this.Controls.Add(this.folderPathText);
            this.Controls.Add(this.selectFolderPath);
            this.Controls.Add(this.saveData);
            this.Controls.Add(this.chart1);
            this.Controls.Add(this.distanceResult);
            this.Controls.Add(this.meassureSwitch);
            this.Controls.Add(this.setAcquisitionRate);
            this.Controls.Add(this.sampleIntervalLabel);
            this.Name = "DistanceMeassureForm";
            this.Text = "光谱干涉测距探头系统";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label sampleIntervalLabel;
        private System.Windows.Forms.ComboBox setAcquisitionRate;
        private System.Windows.Forms.Button meassureSwitch;
        private System.Windows.Forms.Label distanceResult;
        public System.Windows.Forms.DataVisualization.Charting.Chart chart1;
        public System.Windows.Forms.Button saveData;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Button selectFolderPath;
        public System.Windows.Forms.Label folderPathText;
        public System.Windows.Forms.ComboBox SelectChartStyle;
        private System.Windows.Forms.Label ChartLabel;
        private System.Windows.Forms.ComboBox SetExposureTime;
        private System.Windows.Forms.Label ExposureTimeLabel;
        public System.Windows.Forms.CheckBox JumpErrorCalibration;
    }
}
