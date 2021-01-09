using System;
using Thalamus;
using System.Threading;
using NetMQ.Sockets;
using NetMQ;
using GazeOFMessages;
using System.Collections.Generic;
using System.Diagnostics;

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

            public void GazeOpenFace(int faceId, double angleX, double angleY, string target, double timeMiliseconds)
            {
                publisher.GazeOpenFace(faceId, angleX, angleY, target, timeMiliseconds);
            }

            public void TargetCalibrationFinished(int faceId, string target)
            {
                publisher.TargetCalibrationFinished(faceId, target);
            }

            public void TargetCalibrationStarted(int faceId, string target)
            {
                publisher.TargetCalibrationStarted(faceId, target);
            }

            public void CalibrationPhaseFinished(int faceId)
            {
                publisher.CalibrationPhaseFinished(faceId);
            }
        }

        private int id;
        private GazePublisher gPublisher;
        SubscriberSocket socketSubscriber;
        public bool CalibrationPhase;
        private bool calibrateLEFT;
        private bool calibrateRIGHT;
        private int GROUND_TRUTH_SAMPLES = 100;
        List<GazeAngle> leftGroundTruthSamples = new List<GazeAngle>();
        List<GazeAngle> rightGroundTruthSamples = new List<GazeAngle>();
        GazeAngle leftGroundTruth;
        GazeAngle rightGroundTruth;
        Stopwatch stopWatch;
        private Thread MessageDispatcher;
        private Thread CalibrationThread;

        public GazeThalamusClient(string clientName, int faceId) : base(clientName, "SERA")
        {
            id = faceId;

            CalibrationPhase = true;
            calibrateLEFT = false;

            stopWatch = new Stopwatch();
            stopWatch.Start();

            SetPublisher<IGazePublisher>();
            gPublisher = new GazePublisher(Publisher);

            socketSubscriber = new SubscriberSocket();
            socketSubscriber.Connect("tcp://127.0.0.1:5000");
            socketSubscriber.Subscribe("GazeAngle:");
        }

        public override void ConnectedToMaster()
        {
            MessageDispatcher = new Thread(DispatchMessages);
            MessageDispatcher.Start();
            CalibrationThread = new Thread(Calibration);
            CalibrationThread.Start();
        }

        public override void Dispose()
        {
            base.Dispose();
            MessageDispatcher.Join();
            CalibrationThread.Join();
        }

        private GazeAngle ComputeAvg(List<GazeAngle> lst)
        {
            float sumX = 0;
            float sumY = 0;
            foreach (var item in lst)
            {
                sumX += item.X;
                sumY += item.Y;
            }
            int count = lst.Count;
            return new GazeAngle(sumX / count, sumY / count);
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
                gPublisher.TargetCalibrationStarted(id, "LEFT");
                calibrateLEFT = true;
            }
            while (leftGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES) { }
            gPublisher.TargetCalibrationFinished(id, "LEFT");
            calibrateLEFT = false;
            Console.WriteLine("Finished calibrating LEFT target.");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("To calibrate target RIGHT press <r> when the person is looking there.");
            cki = Console.ReadKey();
            if (cki.Key.ToString() == "R")
            {
                gPublisher.TargetCalibrationStarted(id, "RIGHT");
                calibrateRIGHT = true;
            }
            while (rightGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES) { }
            gPublisher.TargetCalibrationFinished(id, "RIGHT");
            calibrateRIGHT = false;
            Console.WriteLine("Finished calibrating RIGHT target.");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("---------------------------------------");

            leftGroundTruth = ComputeAvg(leftGroundTruthSamples);
            rightGroundTruth = ComputeAvg(rightGroundTruthSamples);
            Console.WriteLine("LEFT: " + leftGroundTruth.X + " , " + leftGroundTruth.Y + " RIGHT: " + rightGroundTruth.X + " , " + rightGroundTruth.Y);
            gPublisher.CalibrationPhaseFinished(id);
            CalibrationPhase = false;

        }

        public void DispatchMessages()
        {
            int buffer = 0;
            while (true)
            {
                buffer++;
                if (buffer == 5)
                {
                    buffer = 0;


                    var msg = socketSubscriber.ReceiveFrameString();
                    string[] angles = msg.Substring(10).Replace(" ", "").Split(',');
                    float x = float.Parse(angles[0]) * -1;
                    float y = float.Parse(angles[1]) * -1;

                    if (CalibrationPhase)
                    {
                        if (calibrateLEFT && leftGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES)
                        {
                            Console.WriteLine("Add sample to LEFT: {0}", msg);
                            leftGroundTruthSamples.Add(new GazeAngle(x, y));
                            gPublisher.GazeOpenFace(id, x, y, "LEFT_GROUND_TRUTH", stopWatch.Elapsed.TotalMilliseconds);
                        }
                        else if (calibrateRIGHT && rightGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES)
                        {
                            Console.WriteLine("Add sample to RIGHT: {0}", msg);
                            rightGroundTruthSamples.Add(new GazeAngle(x, y));
                            gPublisher.GazeOpenFace(id, x, y, "RIGHT_GROUND_TRUTH", stopWatch.Elapsed.TotalMilliseconds);
                        }
                    }
                    else //calibration phase ended
                    {
                        double distLEFT = Math.Sqrt(Math.Pow(x - leftGroundTruth.X, 2) + Math.Pow(y - leftGroundTruth.Y, 2));
                        double distRIGHT = Math.Sqrt(Math.Pow(x - rightGroundTruth.X, 2) + Math.Pow(y - rightGroundTruth.Y, 2));
                        //Console.WriteLine("Dist-LEFT: {0}   Dist-RIGHT: {1}", distLEFT, distRIGHT);
                        if (distLEFT < 10 && distRIGHT < 10)
                        {
                            Console.WriteLine("WEIRD CASE 1");
                        }
                        else if (distLEFT < 10)
                        {
                            //Console.WriteLine("<<<<<< LEFT");
                            gPublisher.GazeOpenFace(id, x, y, "left", stopWatch.Elapsed.TotalMilliseconds);
                        }
                        else if (distRIGHT < 10)
                        {
                            //Console.WriteLine(">>>>>> RIGHT");
                            gPublisher.GazeOpenFace(id, x, y, "right", stopWatch.Elapsed.TotalMilliseconds);
                        }
                        else
                        {
                            //Console.WriteLine("!!!! ELSEWHERE !!!!");
                            gPublisher.GazeOpenFace(id, x, y, "elsewhere", stopWatch.Elapsed.TotalMilliseconds);
                        }
                    }
                }
            }
        }
    }
}
