using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trilateration {
    public class Point {
        public double X { get; }
        public double Y { get; }

        public Point(double x, double y) {
            X = x;
            Y = y;
        }

        public override String ToString() {
            return "(" + X + ", " + Y + ")";
        }
    }
}
