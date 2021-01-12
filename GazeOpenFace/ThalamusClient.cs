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
        private int TARGET_THRESHOLD = 80;
        List<GazeAngle> gazeLeftGroundTruthSamples = new List<GazeAngle>();
        List<GazeAngle> gazeRightGroundTruthSamples = new List<GazeAngle>();
        List<HeadPose> headLeftGroundTruthSamples = new List<HeadPose>();
        List<HeadPose> headRightGroundTruthSamples = new List<HeadPose>();
        Point2D leftLocationFromCam;
        Point2D rightLocationFromCam;
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
            socketSubscriber.Subscribe("");
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

        private Point2D ComputeLocationFromCam(GazeAngle ga, HeadPose hp)
        {
            float x = hp.L_Z * (float) Math.Tan(ga.X * Math.PI / 180) + hp.L_X;
            float y = hp.L_Z * (float) Math.Tan(ga.Y * Math.PI / 180) + hp.L_Y;
            return new Point2D(x, y);
        }

        private Point2D ComputeAVGLocationFromCam(List<GazeAngle> gazeAngleList, List<HeadPose> headPoseList)
        {
            float sum_x = 0;
            float sum_y = 0;
            for (int i = 0; i < GROUND_TRUTH_SAMPLES; i++)
            {
                Point2D p = ComputeLocationFromCam(gazeAngleList[i], headPoseList[i]);
                sum_x += p.X;
                sum_y += p.Y;
            }
            return new Point2D(sum_x / GROUND_TRUTH_SAMPLES, sum_y / GROUND_TRUTH_SAMPLES);
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
            while (gazeLeftGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES || headLeftGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES) { }
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
            while (gazeRightGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES || headRightGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES) { }
            gPublisher.TargetCalibrationFinished(id, "RIGHT");
            calibrateRIGHT = false;
            Console.WriteLine("Finished calibrating RIGHT target.");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("---------------------------------------");

            leftLocationFromCam = ComputeAVGLocationFromCam(gazeLeftGroundTruthSamples, headLeftGroundTruthSamples);
            rightLocationFromCam = ComputeAVGLocationFromCam(gazeRightGroundTruthSamples, headRightGroundTruthSamples);
            Console.WriteLine("LEFT: " + leftLocationFromCam.X + " , " + leftLocationFromCam.Y + " RIGHT: " + rightLocationFromCam.X + " , " + rightLocationFromCam.Y);
            gPublisher.CalibrationPhaseFinished(id);
            CalibrationPhase = false;

        }

        public void DispatchMessages()
        {
            float GA_x = 0;
            float GA_y = 0;
            float HL_x;
            float HL_y;
            float HL_z;
            float HR_x;
            float HR_y;
            float HR_z;

            while (true)
            {
                var msg = socketSubscriber.ReceiveFrameString();
                //Console.WriteLine(msg);
                
                string[] firstParse = msg.Split(':');

                if (firstParse[0] == "GazeAngle")
                {
                    string[] gaze = firstParse[1].Replace(", ", "/").Split('/');
                    GA_x = float.Parse(gaze[0]);
                    GA_y = float.Parse(gaze[1]);

                    if (CalibrationPhase)
                    {
                        if (calibrateLEFT && gazeLeftGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES)
                        {
                            Console.WriteLine("Add GAZE sample to LEFT: {0}, {1}", GA_x, GA_y);
                            gazeLeftGroundTruthSamples.Add(new GazeAngle(GA_x, GA_y));
                            //gPublisher.GazeOpenFace(id, GA_x, GA_y, "LEFT_GROUND_TRUTH", stopWatch.Elapsed.TotalMilliseconds);
                        }
                        else if (calibrateRIGHT && gazeRightGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES)
                        {
                            Console.WriteLine("Add GAZE sample to RIGHT: {0}, {1}", GA_x, GA_y);
                            gazeRightGroundTruthSamples.Add(new GazeAngle(GA_x, GA_y));
                            //gPublisher.GazeOpenFace(id, GA_x, GA_y, "RIGHT_GROUND_TRUTH", stopWatch.Elapsed.TotalMilliseconds);
                        }
                    }
                    else
                    {
                    }
                }
                else if (firstParse[0] == "HeadPose")
                {
                    string[] headpose = firstParse[1].Replace(", ", "/").Split('/');
                    HL_x = float.Parse(headpose[0]);
                    HL_y = float.Parse(headpose[1]);
                    HL_z = float.Parse(headpose[2]);
                    HR_x = float.Parse(headpose[3]);
                    HR_y = float.Parse(headpose[4]);
                    HR_z = float.Parse(headpose[5]);

                    if (CalibrationPhase)
                    {

                        if (calibrateLEFT && headLeftGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES)
                        {
                            Console.WriteLine("Add HEAD sample to LEFT: {0},{1},{2} / {3},{4},{5}", HL_x, HL_y, HL_z, HR_x, HR_y, HR_z);
                            headLeftGroundTruthSamples.Add(new HeadPose(HL_x, HL_y, HL_z, HR_x, HR_y, HR_z));
                            //gPublisher.GazeOpenFace(id, GA_x, GA_y, "LEFT_GROUND_TRUTH", stopWatch.Elapsed.TotalMilliseconds);
                        }
                        else if (calibrateRIGHT && headRightGroundTruthSamples.Count < GROUND_TRUTH_SAMPLES)
                        {
                            Console.WriteLine("Add HEAD sample to RIGHT: {0},{1},{2} / {3},{4},{5}", HL_x, HL_y, HL_z, HR_x, HR_y, HR_z);
                            headRightGroundTruthSamples.Add(new HeadPose(HL_x, HL_y, HL_z, HR_x, HR_y, HR_z));
                            //gPublisher.GazeOpenFace(id, GA_x, GA_y, "RIGHT_GROUND_TRUTH", stopWatch.Elapsed.TotalMilliseconds);
                        }
                    }
                    else
                    {
                        GazeAngle newGA = new GazeAngle(GA_x, GA_y);
                        HeadPose newHP = new HeadPose(HL_x, HL_y, HL_z, HR_x, HR_y, HR_z);
                        Point2D newLocationFromCam = ComputeLocationFromCam(newGA, newHP);
                        double distLEFT = Math.Sqrt(Math.Pow(newLocationFromCam.X - leftLocationFromCam.X, 2) + Math.Pow(newLocationFromCam.Y - leftLocationFromCam.Y, 2));
                        double distRIGHT = Math.Sqrt(Math.Pow(newLocationFromCam.X - rightLocationFromCam.X, 2) + Math.Pow(newLocationFromCam.Y - rightLocationFromCam.Y, 2));
                        //Console.WriteLine("Dist-LEFT: {0}   Dist-RIGHT: {1}", distLEFT, distRIGHT);
                        //Console.WriteLine("X,Y: {0},{1}   Left: {2},{3}   Right: {4},{5}", newLocationFromCam.X, newLocationFromCam.Y, leftLocationFromCam.X, leftLocationFromCam.Y, rightLocationFromCam.X, rightLocationFromCam.Y);

                        if (distLEFT <= TARGET_THRESHOLD && distRIGHT <= TARGET_THRESHOLD)
                        {
                            Console.WriteLine("WEIRD CASE");
                        }
                        else if (distLEFT <= TARGET_THRESHOLD)
                        {
                            //Console.WriteLine("LEFT");
                            gPublisher.GazeOpenFace(id, newGA.X, newGA.Y, "left", stopWatch.Elapsed.TotalMilliseconds);
                        }
                        else if (distRIGHT <= TARGET_THRESHOLD)
                        {
                            gPublisher.GazeOpenFace(id, newGA.X, newGA.Y, "right", stopWatch.Elapsed.TotalMilliseconds);
                            //Console.WriteLine("RIGHT");
                        }
                        else
                        {
                            gPublisher.GazeOpenFace(id, newGA.X, newGA.Y, "elsewhere", stopWatch.Elapsed.TotalMilliseconds);
                            //Console.WriteLine("ELSEWHERE");
                        }
                    }
                }
            }
        }
    }
}
