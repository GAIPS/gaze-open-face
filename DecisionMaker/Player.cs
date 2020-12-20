using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DecisionMaker
{
    class Player
    {
        public int ID;
        private string currentGazeTarget;
        private double currentStartingTime;
        private double lastEventTime;
        public double GazeShiftPeriod;
        public int PERIOD_TIME_WINDOW = 5000; //5 seconds
        private List<GazeBehavior> gazeBehaviors;
        private List<GazeEvent> gazeEvents;
        public Thread UpdatesDispatcher;
        public Thread GazeEventsDispatcher;
        public static Mutex mut = new Mutex();
        public bool SessionStarted;

        public Player(int id)
        {
            ID = id;
            currentGazeTarget = "";
            currentStartingTime = 0;
            SessionStarted = false;
            gazeBehaviors = new List<GazeBehavior>();
            gazeEvents = new List<GazeEvent>();
            GazeEventsDispatcher = new Thread(DispacthGazeEvents);
            GazeEventsDispatcher.Start();
            UpdatesDispatcher = new Thread(Updates);
            UpdatesDispatcher.Start();
        }

        public void GazeEvent(string target, double timeMiliseconds)
        {
            GazeEvent ge = new GazeEvent(target, timeMiliseconds);

            mut.WaitOne();
            gazeEvents.Add(ge);
            mut.ReleaseMutex();
        }

        private void Updates()
        {
            while(true)
            {
                if (gazeBehaviors.Count > 0)
                {
                    UpdateGazeShiftRate();
                }
                Thread.Sleep(500);
            }
        }

        private void UpdateGazeShiftRate()
        {
            if (SessionStarted && gazeBehaviors.Count > 0)
            {
                GazeBehavior gb = gazeBehaviors.Last();
                double timeThreshold = lastEventTime - PERIOD_TIME_WINDOW;
                int count = 0;
                for (int i = gazeBehaviors.Count - 1; i >= 0 && gazeBehaviors[i].EndingTime > timeThreshold; i--)
                {
                    if (gazeBehaviors[i].Target == "left" || gazeBehaviors[i].Target == "right")
                    {
                        count++;
                    }
                }
                if (count != 0)
                {
                    GazeShiftPeriod = PERIOD_TIME_WINDOW / count;
                }
                else
                {
                    GazeShiftPeriod = PERIOD_TIME_WINDOW;
                }
                Console.WriteLine("PLAYER " + ID + " - GazeShiftRate " + GazeShiftPeriod + " count: " + count);
            }

        }

        internal void Dispose()
        {
            GazeEventsDispatcher.Join();
            UpdatesDispatcher.Join();
        }

        private void DispacthGazeEvents()
        {
            while (true)
            {
                GazeEvent ge = null;
                mut.WaitOne();
                if (gazeEvents.Count > 0)
                {
                    ge = gazeEvents[0];
                    gazeEvents.RemoveAt(0);
                }
                mut.ReleaseMutex();

                if (ge != null)
                {
                    lastEventTime = ge.Timestamp;
                    if (ge.Target != currentGazeTarget)
                    {
                        //first time
                        if (currentStartingTime == 0)
                        {
                            currentStartingTime = ge.Timestamp;
                            currentGazeTarget = ge.Target;
                        }
                        else //all the time except first
                        {
                            GazeBehavior gb = new GazeBehavior(ID, currentGazeTarget, currentStartingTime, ge.Timestamp);
                            gazeBehaviors.Add(gb);
                            currentStartingTime = ge.Timestamp;
                            currentGazeTarget = ge.Target;
                        }
                    }
                }
            }
        }
    }
}
