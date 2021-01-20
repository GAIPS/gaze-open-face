using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecisionMaker
{
    class GazeBehavior
    {
        public int GazerID;
        public string Target;
        public double StartingTime;
        public double EndingTime;
        public double Duration;

        public GazeBehavior(int id, string target, double startingTime, double endingTime)
        {
            GazerID = id;
            Target = target;
            StartingTime = startingTime;
            EndingTime = endingTime;
            Duration = endingTime - startingTime;
        }

        public GazeBehavior(int id, string target, double startingTime)
        {
            GazerID = id;
            Target = target;
            StartingTime = startingTime;
            EndingTime = 0;
            Duration = 0;
        }

        public void UpdateEndtingTime(double endingTime)
        {
            EndingTime = endingTime;
            Duration = endingTime - StartingTime;
        }

    }
}
