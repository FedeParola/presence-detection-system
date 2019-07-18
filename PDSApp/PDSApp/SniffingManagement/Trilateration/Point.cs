using System;

/// <summary>
/// A point in 2-dimensional space represented by its xy coordinates.
/// </summary>
namespace PDSApp.SniffingManagement.Trilateration {
    public class Point {
        public double X { get; }
        public double Y { get; }

        public Point(double x, double y) {
            X = x;
            Y = y;
        }

        public double Distance(Point p) {
            if (p == null) {
                throw new ArgumentNullException();
            }

            return Math.Sqrt((this.X - p.X) * (this.X - p.X) + (this.Y - p.Y) * (this.Y - p.Y));
        }

        public override String ToString() {
            return "(" + X + ", " + Y + ")";
        }
    }
}
