using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// A point in 2-dimensional space represented by its xy coordinates.
/// </summary>
namespace SniffingManagement.Trilateration {
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
