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
        private string robotTarget;
        private GazeBehavior currentGazeBehaviour;
        private double lastEventTime;
        public double GazeShiftPeriod;
        public double GazeRobotAvgDur;
        public double GazeRobotPeriod;
        public int PERIOD_TIME_WINDOW = 5; //5 seconds
        private List<GazeBehavior> gazeBehaviors;
        private List<GazeEvent> gazeEvents;
        public Thread UpdatesDispatcher;
        public Thread GazeEventsDispatcher;
        public static Mutex mut = new Mutex();
        public bool SessionStarted;
        private List<string> buffer;

        public Player(int id, string targetOfRobot)
        {
            ID = id;
            robotTarget = targetOfRobot;
            currentGazeBehaviour = null;
            SessionStarted = false;
            buffer = new List<string>();
            gazeBehaviors = new List<GazeBehavior>();
            gazeEvents = new List<GazeEvent>();
            GazeEventsDispatcher = new Thread(DispacthGazeEvents);
            GazeEventsDispatcher.Start();
            UpdatesDispatcher = new Thread(Updates);
            UpdatesDispatcher.Start();
        }

        public void GazeEvent(string target, double timeMiliseconds)
        {
            if (currentGazeBehaviour == null || currentGazeBehaviour.Target != target)
            {
                if (buffer.Count > 0 && buffer[0] != target)
                {
                    buffer = new List<string>();
                }
                buffer.Add(target);
            }

            if (buffer.Count == 3)
            {
                buffer = new List<string>();
                GazeEvent ge = new GazeEvent(target, timeMiliseconds);

                mut.WaitOne();
                gazeEvents.Add(ge);
                mut.ReleaseMutex();
            }
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
                int numGazeShifts = 0;
                int numGazeAtRobot = 0;
                double durGazeAtRobot = 0;
                if (currentGazeBehaviour.Target == robotTarget)
                {
                    numGazeAtRobot++;
                    durGazeAtRobot += currentGazeBehaviour.Duration;
                }
                for (int i = gazeBehaviors.Count - 1; i >= 0 && gazeBehaviors[i].EndingTime > timeThreshold; i--)
                {
                    numGazeShifts++;
                    if (gazeBehaviors[i].Target == robotTarget)
                    {
                        numGazeAtRobot++;
                        durGazeAtRobot += gazeBehaviors[i].Duration;
                    }
                }
                if (numGazeShifts != 0)
                {
                    GazeShiftPeriod = PERIOD_TIME_WINDOW / numGazeShifts;
                }
                else
                {
                    GazeShiftPeriod = PERIOD_TIME_WINDOW;
                }
                Console.WriteLine("PLAYER " + ID + " - GazeShiftRate " + GazeShiftPeriod + " count: " + numGazeShifts);

                if (numGazeAtRobot != 0)
                {
                    durGazeAtRobot /= numGazeAtRobot;
                    GazeRobotAvgDur = durGazeAtRobot;
                    GazeRobotPeriod = PERIOD_TIME_WINDOW / numGazeAtRobot;
                }
                else
                {
                    GazeRobotAvgDur = 1;
                    GazeRobotPeriod = PERIOD_TIME_WINDOW;
                }
                Console.WriteLine("PLAYER " + ID + " ------ numGazeAtRobot " + numGazeAtRobot + " durGazeAtRobot: " + durGazeAtRobot);
            }

        }

        internal void Dispose()
        {
            Console.WriteLine("------------------------- gazeBehaviors.size - " + gazeBehaviors.Count);
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
                    
                    //first time
                    if (currentGazeBehaviour == null)
                    {
                        currentGazeBehaviour = new GazeBehavior(ID, ge.Target, ge.Timestamp);
                    }
                    else if(ge.Target != currentGazeBehaviour.Target)
                    {
                        currentGazeBehaviour.UpdateEndtingTime(ge.Timestamp);
                        gazeBehaviors.Add(currentGazeBehaviour);
                        currentGazeBehaviour = new GazeBehavior(ID, ge.Target, ge.Timestamp);
                    }
                    else if (ge.Target == currentGazeBehaviour.Target)
                    {
                        currentGazeBehaviour.UpdateEndtingTime(ge.Timestamp);
                    }
                }
            }
        }
    }
}
