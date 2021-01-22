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
        private Thread mainLoop;
        private Stopwatch stopWatch;
        private int nextGazeShiftEstimate;
        //private long previousGazeShitTime;
        private string currentTarget;
        private bool sessionStarted;

        public DecisionMakerTC() : base("DecisionMaker", "SERA")
        {
            SetPublisher<IGazePublisher>();
            gPublisher = new GazePublisher(Publisher);
            ID = 2;
            player0 = new Player(0);
            player1 = new Player(1);
            currentTarget = "mainscreen";
            nextGazeShiftEstimate = 0;
            stopWatch = new Stopwatch();
            stopWatch.Start();
            mainLoop = new Thread(Update);
            mainLoop.Start();
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
                    if (player0.SessionStarted && player0.CurrentGazeBehaviour != null)
                    {
                        if (player0.CurrentGazeBehaviour.Target == player0.PlayerGazeAtRobot && currentTarget != player0.RobotGazeAtPlayer)
                        {
                            currentTarget = player0.RobotGazeAtPlayer;
                            gPublisher.GazeAtTarget(player0.RobotGazeAtPlayer);
                            Console.WriteLine("------------ gaze at player");
                        }
                        else if (player0.CurrentGazeBehaviour.Target != player0.PlayerGazeAtRobot && currentTarget != player0.CurrentGazeBehaviour.Target)
                        {
                            currentTarget = player0.CurrentGazeBehaviour.Target;
                            gPublisher.GazeAtTarget(player0.CurrentGazeBehaviour.Target);
                            Console.WriteLine("------------ gaze at where player is gazing " + player0.CurrentGazeBehaviour.Target);
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
