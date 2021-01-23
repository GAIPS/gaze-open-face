using System;
using Thalamus;
using GazeOFMessages;
using EmoteCommonMessages;
using System.Threading;
using System.Diagnostics;

namespace DecisionMaker
{

    public interface IGazePublisher : IThalamusPublisher, IGazeStateActions, ITargetEvents { }

    class DecisionMakerTC : ThalamusClient, IGazeOpenFacePerceptions
    {
        private class GazePublisher : IGazePublisher
        {
            dynamic publisher;

            public GazePublisher(dynamic publisher)
            {
                this.publisher = publisher;
            }

            public void GazeAtScreen(double x, double y)
            {
                publisher.GazeAtScreen(x, y);
            }

            public void GazeAtTarget(string targetName)
            {
                publisher.GazeAtTarget(targetName);
            }

            public void GlanceAtScreen(double x, double y)
            {
                publisher.GlanceAtScreen(x, y);
            }

            public void GlanceAtTarget(string targetName)
            {
                publisher.GlanceAtTarget(targetName);
            }

            public void TargetAngleInfo(string targetName, int X, int Y)
            {
                publisher.TargetAngleInfo(targetName, X, Y);
            }

            public void TargetLink(string targetName, string linkedTargetName)
            {
                publisher.TargetLink(targetName, linkedTargetName);
            }

            public void TargetScreenInfo(string targetName, int X, int Y)
            {
                publisher.TargetScreenInfo(targetName, X, Y);
            }

            public void GazeOpenFace(int faceId, float angleX, float angleY, string target)
            {
                publisher.GazeOpenFace(faceId, angleX, angleY, target);
            }
        }

        private GazePublisher gPublisher;
        private int ID;
        private Player player0;
        private Player player1;
        public static Player LastMovingPlayer;
        private Thread mainLoop;
        private Stopwatch stopWatch;
        private int nextGazeShiftEstimate;
        //private long previousGazeShitTime;
        private string currentTarget;
        private bool sessionStarted;
        private int PROACTIVE_THRESHOLD = 3000;//miliseconds
        private Random random;

        public DecisionMakerTC() : base("DecisionMaker", "SERA")
        {
            SetPublisher<IGazePublisher>();
            gPublisher = new GazePublisher(Publisher);
            ID = 2;
            player0 = new Player(0);
            player1 = new Player(1);
            LastMovingPlayer = player0;
            currentTarget = "mainscreen";
            nextGazeShiftEstimate = 0;
            stopWatch = new Stopwatch();
            stopWatch.Start();
            mainLoop = new Thread(Update);
            mainLoop.Start();
            random = new Random();
        }

        public override void Dispose()
        {
            base.Dispose();
            player0.Dispose();
            player1.Dispose();
            mainLoop.Join();
        }

        private void Update()
        {
            while(true)
            {
                if (sessionStarted)
                {
                    if (player0.SessionStarted && player0.CurrentGazeBehaviour != null && player1.SessionStarted && player1.CurrentGazeBehaviour != null)
                    {
                        //reactive
                        if (LastMovingPlayer.IsGazingAtRobot() && currentTarget != LastMovingPlayer.Name)
                        {
                            currentTarget = LastMovingPlayer.Name;
                            gPublisher.GazeAtTarget(LastMovingPlayer.Name);
                            stopWatch.Restart();
                            Console.WriteLine("------------ gaze back " + LastMovingPlayer.Name);
                        }
                        else if (!LastMovingPlayer.IsGazingAtRobot() && LastMovingPlayer.CurrentGazeBehaviour.Target != "elsewhere" && currentTarget != LastMovingPlayer.CurrentGazeBehaviour.Target)
                        {
                            currentTarget = LastMovingPlayer.CurrentGazeBehaviour.Target;
                            gPublisher.GazeAtTarget(LastMovingPlayer.CurrentGazeBehaviour.Target);
                            stopWatch.Restart();
                            Console.WriteLine("------------ gaze at where " + LastMovingPlayer.Name + " is gazing " + LastMovingPlayer.CurrentGazeBehaviour.Target);
                        }

                        //proactive
                        if (stopWatch.ElapsedMilliseconds > PROACTIVE_THRESHOLD)
                        {
                            string newTarget = "";
                            if (currentTarget == "mainscreen")
                            {
                                bool shouldLookAtP0 = player0.GazeRobotPeriod < player0.PERIOD_TIME_WINDOW && stopWatch.ElapsedMilliseconds > player0.GazeRobotPeriod;
                                bool shouldLookAtP1 = player1.GazeRobotPeriod < player1.PERIOD_TIME_WINDOW && stopWatch.ElapsedMilliseconds > player1.GazeRobotPeriod;
                                if (shouldLookAtP0 && shouldLookAtP1)
                                {
                                    int randomize = random.Next(2);
                                    if (randomize == 0)
                                    {
                                        newTarget = player0.Name;
                                    }
                                    else
                                    {
                                        newTarget = player1.Name;
                                    }
                                }
                                else if (shouldLookAtP0)
                                {
                                    newTarget = player0.Name;
                                }
                                else if (shouldLookAtP1)
                                {
                                    newTarget = player1.Name;
                                }
                            }
                            else if (currentTarget == player0.Name)
                            {
                                if (player1.GazeRobotPeriod < player1.PERIOD_TIME_WINDOW && stopWatch.ElapsedMilliseconds > player1.GazeRobotPeriod)
                                {
                                    newTarget = player1.Name;
                                }
                            }
                            else if (currentTarget == player1.Name)
                            {
                                if (player0.GazeRobotPeriod < player0.PERIOD_TIME_WINDOW && stopWatch.ElapsedMilliseconds > player0.GazeRobotPeriod)
                                {
                                    newTarget = player0.Name;
                                }
                            }

                            if (newTarget != "")
                            {
                                currentTarget = newTarget;
                                gPublisher.GazeAtTarget(newTarget);
                                stopWatch.Restart();
                                Console.WriteLine("------PROACTIVE------ gaze at " + newTarget);
                            }
                        }
                    }
                }
            }
        }

        public void GazeOpenFace(int faceId, double angleX, double angleY, string target, double timeMiliseconds)
        {
            if (faceId != ID && sessionStarted)
            {
                if (player0.ID == faceId)
                {
                    player0.GazeEvent(target, timeMiliseconds);
                }
                else if (player1.ID == faceId)
                {
                    player1.GazeEvent(target, timeMiliseconds);
                }

            }
        }

        public void TargetCalibrationStarted(int faceId, string target)
        {
            //DO SOMETHING
        }

        public void TargetCalibrationFinished(int faceId, string target)
        {
            //DO SOMETHING
        }

        public void CalibrationPhaseFinished(int faceId)
        {
            if (faceId == player0.ID)
            {
                player0.SessionStarted = true;
                sessionStarted = true;
            }
            else if (faceId == player1.ID)
            {
                player1.SessionStarted = true;
            }

            if (player0.SessionStarted && player1.SessionStarted)
            {
                sessionStarted = true;
            }
        }
    }
}
