using System;
using Thalamus;
using System.Threading;
using NetMQ.Sockets;
using NetMQ;
using EmoteCommonMessages;

namespace GazeOpenFace
{

    public interface IGazePublisher : IThalamusPublisher, IGazeStateActions, ITargetEvents { }

    class GazeThalamusClient : ThalamusClient
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
        }

        private GazePublisher gPublisher;
        SubscriberSocket socketSubscriber;

        public GazeThalamusClient() : base("GazeOpenFace", "SERA")
        {
            SetPublisher<IGazePublisher>();
            gPublisher = new GazePublisher(Publisher);

            socketSubscriber = new SubscriberSocket();
            socketSubscriber.Connect("tcp://127.0.0.1:5000");
            socketSubscriber.Subscribe("GazeAngle:");
        }

        public override void ConnectedToMaster()
        {
            Thread thread = new Thread(DispatchMessages);
            thread.Start();
        }

        public void DispatchMessages()
        {
            int i = 0;
            while (true)
            {
                var msg = socketSubscriber.ReceiveFrameString();
                //Console.WriteLine("From OpenFace: {0}", msg);
                /*i++;
                if (i == 25)
                {
                    i = 0;*/

                    string[] angles = msg.Substring(10).Replace(" ", "").Split(',');
                    int x = (int) Math.Round(float.Parse(angles[0])) * -1;
                    int y = (int) Math.Round(float.Parse(angles[1])) * -1;
                    Console.WriteLine("From OpenFace: {0}", msg);
                    gPublisher.TargetAngleInfo("test", x, y);
                    gPublisher.GazeAtTarget("test");
                //}
            }
        }
    }
}
