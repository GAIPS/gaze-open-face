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
        private Thread mainLoop;
        private Stopwatch stopWatch;
        private long nextGazeShiftEstimate;
        private long previousGazeShitTime;
        private string currentTarget;
        private bool sessionStarted;

        public DecisionMakerTC() : base("DecisionMaker", "SERA")
        {
            SetPublisher<IGazePublisher>();
            gPublisher = new GazePublisher(Publisher);
            ID = 1;
            player0 = new Player(0);
            currentTarget = "left";
            stopWatch = new Stopwatch();
            stopWatch.Start();
            mainLoop = new Thread(Update);
            mainLoop.Start();
        }

        public override void Dispose()
        {
            base.Dispose();
            player0.Dispose();
            mainLoop.Join();
        }

        private void Update()
        {
            while(true)
            {
                if (sessionStarted)
                {
                    if (stopWatch.ElapsedMilliseconds >= nextGazeShiftEstimate)
                    {
                        if (currentTarget == "right")
                        {
                            currentTarget = "left";
                            gPublisher.GazeAtTarget(currentTarget);
                        }
                        else
                        {
                            currentTarget = "right";
                            gPublisher.GazeAtTarget(currentTarget);
                        }
                        previousGazeShitTime = stopWatch.ElapsedMilliseconds;
                    }
                    nextGazeShiftEstimate = previousGazeShitTime + (long)player0.GazeShiftPeriod;
                    Thread.Sleep(500);
                }
            }
        }

        public void GazeOpenFace(int faceId, double angleX, double angleY, string target, double timeMiliseconds)
        {
            if (faceId != ID)
            {
                if (player0.ID == faceId && (target == "left" || target == "right"))
                {
                    player0.GazeEvent(target, timeMiliseconds);
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

        public void CalibrationPhaseFinished()
        {
            sessionStarted = true;
            player0.SessionStarted = true;
        }
    }
}
