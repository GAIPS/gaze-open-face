using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecisionMaker
{
    class GazeEvent
    {
        public string Target;
        public double Timestamp;

        public GazeEvent(string target, double timestamp)
        {
            Target = target;
            Timestamp = timestamp;
        }
    }
}
