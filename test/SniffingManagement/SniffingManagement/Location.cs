using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SniffingManagement.Trilateration;

/// <summary>
/// A position of a device in a particular instant
/// </summary>
namespace SniffingManagement
{
    class Location
    { 
        public Point Position
        {
            get;
        }

        public long Timestamp
        {
            get;
        }
        public Location(Point position, long timestamp)
        {
            Position = position;
            Timestamp = timestamp;
        }

        public override String ToString()
        {
            return Position.ToString() + " T: " + Timestamp.ToString();
        }
    }
}