using PDSApp.Persistence;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PDSApp.GUI {
    /// <summary>
    /// Interaction logic for UserControlHidden.xaml
    /// </summary>
    public partial class UserControlHidden : UserControl {
        /* Extract the sequence number from the sequnece control field of a packet */
        private const int SEQ_NUMBER_MASK = 4095; // 0x0FFF
        /* Average packets ratio of a device (p/s) */
        private const double NORMAL_PACKETS_RATIO = 600;
        /* Maximum deviation from the normal packets ratio to consider two packets of the same device (p/s) */
        private const double RATIO_DEVIATION_THRESHOLD = 600;
        /* Maximum time lapse between two packets to consider them of the same device (ms) */
        private const double TIME_THRESHOLD = 10 * 60 * 1000;
        /* Maximum movement speed to consider a packet of the same device (m/s) */
        private const double SPEED_THRESHOLD = 1;
        /* With the following constants the probability functions return 0.5 for the given thresholds */
        private readonly double Ks = -Math.Log(0.5) / SPEED_THRESHOLD;
        private readonly double Kr = -Math.Log(0.5) / RATIO_DEVIATION_THRESHOLD;


        public UserControlHidden() {
            InitializeComponent();
        }

        private void Estimate_Click(object sender, RoutedEventArgs e) {
            /* Read input data */
            if (dtpStart.Value.IsNull() || dtpStop.Value.IsNull()) {
                MessageBox.Show("Set the time interval", "Invalid input");
                return;
            }

            DateTime startInstant = dtpStart.Value.Value;
            DateTime stopInstant = dtpStop.Value.Value;

            if (stopInstant <= startInstant) {
                MessageBox.Show("Invalid time interval", "Invalid input");
                return;
            }

            int addrsCount = App.AppDBManager.GetLocalAddressesCount(DateToMillis(startInstant), DateToMillis(stopInstant));

            List<Packet> packets = App.AppDBManager.GetLocalPackets(DateToMillis(startInstant), DateToMillis(stopInstant));

            /* Analyze local packets */
            bool[] checkedP = new bool[packets.Count]; // Initialized to false by default
            List<double> errors = new List<double>();
            int devicesCount = 0;
            int current;

            for (int i = 0; i < packets.Count; i++) {
                /* Analyze packets that haven't been checked yet */
                if (!checkedP[i]) {
                    current = i;
                    checkedP[current] = true;
                    devicesCount++;

                    /* Look for other packets of the same device */
                    bool nextPacketFound = true;
                    while (nextPacketFound) {
                        /* Look for other packets with the same MAC address in the following minutes */
                        for (int j = current + 1; j < packets.Count; j++) {
                            if (!checkedP[j] && packets[current].MacAddr.Equals(packets[j].MacAddr)) {
                                checkedP[j] = true;
                                current = j;
                            }
                        }

                        /* 
                         * Look for a possible new MAC address, three factors are taken into account:
                         * - The time lapse between the packets, if it's too big we don't have enough data to link them
                         * - The hypothetic movement speed of the device
                         * - The deviation from a normal packets ratio
                         */
                        nextPacketFound = false;
                        long timeLapse = 0;
                        for (int j = current + 1; j < packets.Count && timeLapse < TIME_THRESHOLD && !nextPacketFound; j++) {
                            if (!checkedP[j]) {
                                timeLapse = packets[j].Timestamp - packets[current].Timestamp;
                                double speed = packets[current].Position.Distance(packets[j].Position) / timeLapse * 1000;
                                double ratioDeviation = Math.Abs(ComputeRatio(packets[current], packets[j]) - NORMAL_PACKETS_RATIO);
                                
                                if (timeLapse < TIME_THRESHOLD && speed <= SPEED_THRESHOLD && ratioDeviation < RATIO_DEVIATION_THRESHOLD) {
                                    current = j;
                                    checkedP[current] = true;
                                    nextPacketFound = true;

                                    /* Compute errors */
                                    errors.Add(1 - Math.Exp(-Ks * speed)); // Speed error
                                    errors.Add(1 - Math.Exp(-Kr * ratioDeviation));// Packets ratio error
                                }
                            }
                        }
                    }
                }
            }

            double avgError = 0;
            if (errors.Count > 0) {
                errors.ForEach((err) => avgError += err);
                avgError = avgError / errors.Count;
            }

            /* Display results */
            lblAddrsCount.Content = addrsCount;
            lblDevsCount.Content = devicesCount;
            lblError.Content = String.Format("{0:0.00}", avgError *100) + " %";
        }

        private double ComputeRatio(Packet first, Packet second) {
            int firstSeq = first.SequenceCtrl & SEQ_NUMBER_MASK;
            int secondSeq = second.SequenceCtrl & SEQ_NUMBER_MASK;
            double diff;

            if (secondSeq > firstSeq) {
                diff = secondSeq - firstSeq;
            } else {
                diff = secondSeq + SEQ_NUMBER_MASK - firstSeq;
            }

            return diff / (second.Timestamp - first.Timestamp) * 1000; // p/s
        }

        private long DateToMillis(DateTime date) {
            return new DateTimeOffset(date).ToUnixTimeMilliseconds();
        }
    }
}
