using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PDSApp.GUI {
    /// <summary>
    /// Logica di interazione per UserControlStat.xaml
    /// </summary>
    public partial class UserControlStat : UserControl {
        private const int RT_CHART_POINTS_COUNT = 10;
        private const int LT_CHART_INTERVALS = 10;
        private const int LT_CHART_MIN_INTERVAL_SIZE_MILLIS = 60*000;

        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> DateTimeFormatter { get; set; }

        private ChartValues<Tuple<DateTime, int>> detectedDevicesCountValues;
        private int timeInterval;
        private DispatcherTimer chartRefreshTimer = new DispatcherTimer();

        private List<string> talkativeDevices;


        public UserControlStat() {
            InitializeComponent();

            /* Converts a Date-DetectedDevices pair into chart X, Y values */
            Charting.For<Tuple<DateTime, int>>(Mappers.Xy<Tuple<DateTime, int>>()
                .X(model => model.Item1.Ticks)
                .Y(model => model.Item2));

            /* Converts a MAC-PacketsCount pair into chart X, Y values */
            Charting.For<Tuple<string, int>>(Mappers.Xy<Tuple<string, int>>()
                .X(model => talkativeDevices.IndexOf(model.Item1))
                .Y(model => model.Item2));

            SeriesCollection = new SeriesCollection();

            chartRefreshTimer.Tick += ChartRefreshTimer_Tick;

            /* Stop the timer when the Control is unloaded */
            this.Unloaded += (object sender, RoutedEventArgs e) => {
                if (chartRefreshTimer.IsEnabled) {
                    chartRefreshTimer.Stop();
                }
            };

            DataContext = this;
        }

        private void Real_Time_Button_Click(object sender, RoutedEventArgs e) {
            if (!App.AppSniffingManager.IsSniffing()){
                MessageBox.Show("You must first activate the sniffing process!", "Invalid action");
                return;
            }

            /* Clear old series and timer (if running) */
            SeriesCollection.Clear();
            if (chartRefreshTimer.IsEnabled) {
                chartRefreshTimer.Stop();
            }

            timeInterval = Convert.ToInt32(timeIntervalPicker.Text)*60*1000;
            
            /* Prepare axis */
            yAxis.Title = "Detected Devices Count";
            xAxis.Title = "Time";
            xAxis.Labels = null;
            xAxis.LabelFormatter = (value) => new DateTime((long) value).ToString("HH:mm:ss");

            /* Prepare the starting series: devices count for the preceding 9 time instants is set to 0 */
            detectedDevicesCountValues = new ChartValues<Tuple<DateTime, int>>();

            DateTime time = DateTime.Now;
            time = time.AddSeconds(-(RT_CHART_POINTS_COUNT-1) * App.AppSniffingManager.SniffingPeriod);

            for (int i = 0; i < (RT_CHART_POINTS_COUNT-1); i++) {
                detectedDevicesCountValues.Add(new Tuple<DateTime, int>(time, 0));
                time = time.AddSeconds(App.AppSniffingManager.SniffingPeriod);
            }

            int val = App.AppDBManager.CountDetectedDevices(timeInterval);
            detectedDevicesCountValues.Add(new Tuple<DateTime, int>(time, val));

            SeriesCollection.Add(new LineSeries {
                Title = "Detected Devices",
                Values = detectedDevicesCountValues,
                LineSmoothness = 0
            });

            /* Program the timer */
            chartRefreshTimer.Interval = new TimeSpan(App.AppSniffingManager.SniffingPeriod * 10000000);
            chartRefreshTimer.Start();
        }

        private void ChartRefreshTimer_Tick(object sender, EventArgs e) {
            int val = App.AppDBManager.CountDetectedDevices(timeInterval);
            detectedDevicesCountValues.Add(new Tuple<DateTime, int>(DateTime.Now, val));
            detectedDevicesCountValues.RemoveAt(0);
        }

        private void Long_Term_Button_Click(object sender, RoutedEventArgs e) {
            /* Read input data */
            if (dtpStart.Value.IsNull() || dtpStop.Value.IsNull()) {
                MessageBox.Show("Set the time interval", "Invalid input");
                return;
            }

            DateTime startInstant = dtpStart.Value.Value;
            DateTime stopInstant = dtpStop.Value.Value;
            int devNum = Convert.ToInt32(DevNumPickerCol.Text);

            if ((stopInstant - startInstant).TotalMilliseconds < LT_CHART_MIN_INTERVAL_SIZE_MILLIS) {
                MessageBox.Show("Invalid time interval", "Invalid input");
                return;
            }

            /* Clear old series and timer (if running) */
            SeriesCollection.Clear();
            if (chartRefreshTimer.IsEnabled) {
                chartRefreshTimer.Stop();
            }

            /* Prepare axis */
            yAxis.Title = "Packets Sent";
            xAxis.Title = "Device MAC";
            xAxis.LabelFormatter = null; // Labels are set statically

            /* Define the size of time intervals */
            long intervalSize = (long)(stopInstant - startInstant).TotalMilliseconds / LT_CHART_INTERVALS;

            /* Make sure the interval size isn't less than LT_CHART_MIN_INTERVAL_SIZE_MILLIS */
            if (intervalSize < LT_CHART_MIN_INTERVAL_SIZE_MILLIS) {
                intervalSize = LT_CHART_MIN_INTERVAL_SIZE_MILLIS;
            }

            talkativeDevices = App.AppDBManager.GetTalkativeDevices(DateToMillis(startInstant), DateToMillis(stopInstant), devNum);

            var detections = App.AppDBManager
                .GetTalkativeDevices(DateToMillis(startInstant), DateToMillis(stopInstant), intervalSize, devNum);

            long currentInterval = DateToMillis(startInstant) - DateToMillis(startInstant) % intervalSize;
            long lastInterval = DateToMillis(stopInstant) - DateToMillis(stopInstant) % intervalSize;

            /* Scan all intervals */
            while (currentInterval <= lastInterval) {
                if (detections.ContainsKey(currentInterval)) {
                    var chartValues = new ChartValues<Tuple<string, int>>(detections[currentInterval]);

                    SeriesCollection.Add(new StackedColumnSeries {
                        Title = GetIntervalName(currentInterval, intervalSize) + ":",
                        Values = chartValues,
                        StackMode = StackMode.Values,
                        DataLabels = true
                    });
                }

                /* Go to next interval */
                currentInterval += intervalSize;
            }

            xAxis.Labels = talkativeDevices.ToArray();
        }

        private string GetIntervalName(long intervalStart, long intervalSize) {
            string f = "yyyy-MM-dd HH:mm";
            return MillisToDate(intervalStart).ToString(f)
                + " to "
                + MillisToDate(intervalStart + intervalSize).ToString(f);
        }

        private long DateToMillis(DateTime date) {
            return new DateTimeOffset(date).ToUnixTimeMilliseconds();
        }

        private DateTime MillisToDate(long millis) {
            return DateTimeOffset.FromUnixTimeMilliseconds(millis).LocalDateTime;
        }
    }
}
