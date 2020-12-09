using System;
using Thalamus;
using System.Threading;
using NetMQ.Sockets;
using NetMQ;
using GazeOFMessages;
using System.Collections.Generic;

namespace GazeOpenFace
{

    public interface IGazePublisher : IThalamusPublisher, IGazeOpenFacePerceptions { }

    class GazeThalamusClient : ThalamusClient
    {
        private class GazePublisher : IGazePublisher
        {
            dynamic publisher;

            public GazePublisher(dynamic publisher)
            {
                this.publisher = publisher;
            }

            public void GazeOpenFace(int faceId, double angleX, double angleY, string target)
            {
                publisher.GazeOpenFace(faceId, angleX, angleY, target);
            }

            public void TargetCalibrationFinished(int faceId, string target)
            {
                publisher.TargetCalibrationFinished(faceId, target);
            }

            public void TargetCalibrationStarted(int faceId, string target)
            {
                publisher.TargetCalibrationStarted(faceId, target);
            }
        }

        private GazePublisher gPublisher;
        SubscriberSocket socketSubscriber;
        public bool CalibrationPhase;
        private bool calibrateLEFT;
        private bool calibrateRIGHT;
        private int GROUND_TRUTH_SAMPLES = 100;
        List<GazeAngle> leftGroundTruth = new List<GazeAngle>();
        List<GazeAngle> rightGroundTruth = new List<GazeAngle>();

        public GazeThalamusClient() : base("GazeOpenFace", "SERA")
        {
            CalibrationPhase = true;
            calibrateLEFT = false;

            SetPublisher<IGazePublisher>();
            gPublisher = new GazePublisher(Publisher);

            socketSubscriber = new SubscriberSocket();
            socketSubscriber.Connect("tcp://127.0.0.1:5000");
            socketSubscriber.Subscribe("GazeAngle:");
        }

        public override void ConnectedToMaster()
        {
            Thread thread1 = new Thread(DispatchMessages);
            thread1.Start();
            Thread thread2 = new Thread(Calibration);
            thread2.Start();
        }

        public void Calibration()
        {
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("-------------- CALIBRATION ------------");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("To calibrate target LEFT press <l> when the person is looking there.");
            ConsoleKeyInfo cki = Console.ReadKey();
            if (cki.Key.ToString() == "L")
            {
                gPublisher.TargetCalibrationStarted(0, "LEFT");
                calibrateLEFT = true;
            }
            while (leftGroundTruth.Count < GROUND_TRUTH_SAMPLES) { }
            gPublisher.TargetCalibrationFinished(0, "LEFT");
            calibrateLEFT = false;
            Console.WriteLine("Finished calibrating LEFT target.");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("To calibrate target RIGHT press <r> when the person is looking there.");
            cki = Console.ReadKey();
            if (cki.Key.ToString() == "R")
            {
                calibrateRIGHT = true;
            }
            while (rightGroundTruth.Count < GROUND_TRUTH_SAMPLES) { }
            calibrateRIGHT = false;
            Console.WriteLine("Finished calibrating RIGHT target.");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("---------------------------------------");
            CalibrationPhase = false;

        }

        public void DispatchMessages()
        {
            while (true)
            {
                var msg = socketSubscriber.ReceiveFrameString();
                string[] angles = msg.Substring(10).Replace(" ", "").Split(',');
                float x = float.Parse(angles[0]) * -1;
                float y = float.Parse(angles[1]) * -1;

                if (CalibrationPhase)
                {
                    if (calibrateLEFT && leftGroundTruth.Count < GROUND_TRUTH_SAMPLES)
                    {
                        Console.WriteLine("Add sample to LEFT: {0}", msg);
                        leftGroundTruth.Add(new GazeAngle(x, y));
                    }
                    else if (calibrateRIGHT && rightGroundTruth.Count < GROUND_TRUTH_SAMPLES)
                    {
                        Console.WriteLine("Add sample to RIGHT: {0}", msg);
                        rightGroundTruth.Add(new GazeAngle(x, y));
                    }
                }
                else //calibration phase ended
                {
                    Console.WriteLine("From OpenFace: {0}", msg);
                    gPublisher.GazeOpenFace(0, x, y, "left");
                }
            }
        }
    }
}
