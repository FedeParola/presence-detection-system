using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trilateration {
    class TrilaterationCalculator {
        private List<Measurement> measurements = new List<Measurement>();

        public void AddMeasurement(Measurement m) {
            if(m != null) {
                measurements.Add(m);
            }
        }

        private double Distance(double x0, double y0, double x1, double y1) {
            return Math.Sqrt((x0-x1)*(x0-x1) + (y0-y1)*(y0-y1));
        }

        private void Residues(double[] x, double[] fi, object obj) {
            for (int i = 0; i < measurements.Count; i++) {
                fi[i] = Distance(x[0], x[1], measurements[i].Origin.X, measurements[i].Origin.Y) - measurements[i].Distance;
            }
        }

        public Point Compute() {
            /* Check if there are enough measurements */
            if (measurements.Count < 2) {
                return null;
            }

            double[] x = new double[] { 0, 0 }; // Starting point
            double epsx = 0.0000000001;         // Stop criterion
            alglib.minlmstate state;
            alglib.minlmreport rep;

            /*
             * Create optimizer, tell it to:
             * - use numerical differentiation with step equal to 0.0001
             * - stop after short enough step (less than epsx)
             */
            alglib.minlmcreatev(measurements.Count, x, 0.0001, out state);
            alglib.minlmsetcond(state, epsx, 0);

            /* Optimize */
            alglib.minlmoptimize(state, Residues, null, null);

            /* Get optimization results */
            alglib.minlmresults(state, out x, out rep);

            return new Point(x[0], x[1]);
        }
    }
}