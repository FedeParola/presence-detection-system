using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trilateration {
    class Program {
        static void Main(string[] args) {
            TrilaterationCalculator tc = new TrilaterationCalculator();

            tc.AddMeasurement(new Measurement(new Point(0, 0), 70.71));
            tc.AddMeasurement(new Measurement(new Point(100, 0), 70.71));
            tc.AddMeasurement(new Measurement(new Point(0, 100), 70.71));

            Point res = tc.Compute();

            Console.WriteLine(res.ToString()); // Expected (50, 50)
            Console.Read();
        }
    }
}
