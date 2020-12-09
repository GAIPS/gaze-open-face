using System;
using Thalamus;
using GazeOFMessages;
using EmoteCommonMessages;

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

        public DecisionMakerTC() : base("DecisionMaker", "SERA")
        {
            SetPublisher<IGazePublisher>();
            gPublisher = new GazePublisher(Publisher);
        }

        public void GazeOpenFace(int faceId, double angleX, double angleY, string target)
        {
            //DO SOMETHING
        }

        public void TargetCalibrationStarted(int faceId, string target)
        {
            //DO SOMETHING
        }

        public void TargetCalibrationFinished(int faceId, string target)
        {
            //DO SOMETHING
        }
    }
}
