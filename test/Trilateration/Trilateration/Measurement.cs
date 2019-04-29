using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// A measurement of distance from a point in 2-dimensional space.
/// </summary>
namespace Trilateration {
    class Measurement {
        public Point Origin { get; }
        public double Distance { get; }

        public Measurement(Point origin, double distance) {
            Origin = origin;
            Distance = distance;
        }
    }
}
