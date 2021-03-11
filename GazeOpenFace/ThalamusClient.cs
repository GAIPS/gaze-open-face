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
        public int currentTargetBeingCalibrated;
        private int GROUND_TRUTH_SAMPLES = 30;
        private int DIST_THRESHOLD = 80;
        List<GazeTarget> gazeTargets;
        Stopwatch stopWatch;
        private Thread MessageDispatcher;
        private Thread CalibrationThread;
        private string currentTarget;

        enum Targets
        {
            PLAYER_A = 0,
            PLAYER_B = 1,
            MAINSCREEN = 2
        }

        public GazeThalamusClient(string clientName, int faceId) : base(clientName, "SERA")
        {
            id = faceId;
            currentTarget = "";

            CalibrationPhase = true;
            gazeTargets = new List<GazeTarget>();
            // ADD TARGETS IN THE SAME ORDER of enum Targets
            if (faceId == 0)
            {
                gazeTargets.Add(new GazeTarget("player1", GROUND_TRUTH_SAMPLES, 100));
            }
            else if (faceId == 1)
            {
                gazeTargets.Add(new GazeTarget("player0", GROUND_TRUTH_SAMPLES, 100));
            }
            gazeTargets.Add(new GazeTarget("player2", GROUND_TRUTH_SAMPLES, 100));
            gazeTargets.Add(new GazeTarget("mainscreen", GROUND_TRUTH_SAMPLES, 100));

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

        public void Calibration()
        {
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("-------------- CALIBRATION ------------");
            Console.WriteLine("---------------------------------------");
            for (int i = 0; i < gazeTargets.Count; i++)
            {
                Console.WriteLine("To calibrate target " + gazeTargets[i].Name + " press <C> when the person is looking there.");
                ConsoleKeyInfo cki = Console.ReadKey();
                if (cki.Key.ToString() == "C")
                {
                    currentTargetBeingCalibrated = i;
                    gPublisher.TargetCalibrationStarted(id, gazeTargets[i].Name);
                    gazeTargets[i].IsCalibrationStarted = true;
                }
                while (!gazeTargets[i].IsCalibrationFinished) { }
                gazeTargets[i].ComputeAVGLocationFromCam();
                gPublisher.TargetCalibrationFinished(id, gazeTargets[i].Name);
                Console.WriteLine("Finished calibrating " + gazeTargets[i].Name + " target.");
                Console.WriteLine("---------------------------------------");
            }
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("---------------------------------------");

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

            int buffer = 0;

            while (true)
            {
                buffer++;
                var msg = socketSubscriber.ReceiveFrameString();
                //Console.WriteLine(msg);
                if (buffer == 1)
                {
                    buffer = 0;
                    string[] firstParse = msg.Split(':');

                    if (firstParse[0] == "GazeAngle")
                    {
                        string[] gaze = firstParse[1].Replace(", ", "/").Split('/');
                        GA_x = float.Parse(gaze[0]);
                        GA_y = float.Parse(gaze[1]);

                        if (CalibrationPhase)
                        {
                            if (gazeTargets[currentTargetBeingCalibrated].IsCalibrationStarted && !gazeTargets[currentTargetBeingCalibrated].IsGazeDirEnoughSamples())
                            {
                                gazeTargets[currentTargetBeingCalibrated].AddGazeDirSample(new GazeAngle(GA_x, GA_y));
                                Console.WriteLine("Add GAZE sample to " + gazeTargets[currentTargetBeingCalibrated].Name + ": {0}, {1}", GA_x, GA_y);
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
                            if (gazeTargets[currentTargetBeingCalibrated].IsCalibrationStarted && !gazeTargets[currentTargetBeingCalibrated].IsHeadPoseEnoughSamples())
                            {
                                gazeTargets[currentTargetBeingCalibrated].AddHeadPoseSample(new HeadPose(HL_x, HL_y, HL_z, HR_x, HR_y, HR_z));
                                Console.WriteLine("Add HEAD sample to " + gazeTargets[currentTargetBeingCalibrated].Name + ": {0},{1},{2} / {3},{4},{5}", HL_x, HL_y, HL_z, HR_x, HR_y, HR_z);
                            }
                        }
                        else
                        {
                            GazeAngle newGA = new GazeAngle(GA_x, GA_y);
                            HeadPose newHP = new HeadPose(HL_x, HL_y, HL_z, HR_x, HR_y, HR_z);
                            Point2D newLocationFromCam = GazeTarget.ComputeLocationFromCam(newGA, newHP);

                            double distPlayerA = gazeTargets[(int)Targets.PLAYER_A].DistanceFromPoint(newLocationFromCam);
                            double distPlayerB = gazeTargets[(int)Targets.PLAYER_B].DistanceFromPoint(newLocationFromCam);
                            double distMAINSCREEN = gazeTargets[(int)Targets.MAINSCREEN].DistanceFromPoint(newLocationFromCam);
                            //Console.WriteLine("Dist-LEFT: {0}   Dist-RIGHT: {1}", distLEFT, distRIGHT);
                            //Console.WriteLine("X,Y: {0},{1}   Left: {2},{3}   Right: {4},{5}", newLocationFromCam.X, newLocationFromCam.Y, leftLocationFromCam.X, leftLocationFromCam.Y, rightLocationFromCam.X, rightLocationFromCam.Y);

                            if (gazeTargets[(int)Targets.PLAYER_A].IsLookingAtTarget(distPlayerA) && gazeTargets[(int)Targets.PLAYER_B].IsLookingAtTarget(distPlayerB))
                            {
                                Console.WriteLine("WEIRD CASE");
                            }
                            else if (currentTarget != gazeTargets[(int)Targets.MAINSCREEN].Name && gazeTargets[(int)Targets.MAINSCREEN].IsLookingAtTarget(distMAINSCREEN))
                            {
                                currentTarget = gazeTargets[(int)Targets.MAINSCREEN].Name;
                                Console.WriteLine("MAINSCREEN");
                                gPublisher.GazeOpenFace(id, newGA.X, newGA.Y, gazeTargets[(int)Targets.MAINSCREEN].Name, stopWatch.Elapsed.TotalSeconds);
                            }
                            else if (currentTarget != gazeTargets[(int)Targets.PLAYER_A].Name && gazeTargets[(int)Targets.PLAYER_A].IsLookingAtTarget(distPlayerA))
                            {
                                currentTarget = gazeTargets[(int)Targets.PLAYER_A].Name;
                                Console.WriteLine(gazeTargets[(int)Targets.PLAYER_A].Name);
                                gPublisher.GazeOpenFace(id, newGA.X, newGA.Y, gazeTargets[(int)Targets.PLAYER_A].Name, stopWatch.Elapsed.TotalSeconds);
                            }
                            else if (currentTarget != gazeTargets[(int)Targets.PLAYER_B].Name && gazeTargets[(int)Targets.PLAYER_B].IsLookingAtTarget(distPlayerB))
                            {
                                currentTarget = gazeTargets[(int)Targets.PLAYER_B].Name;
                                gPublisher.GazeOpenFace(id, newGA.X, newGA.Y, gazeTargets[(int)Targets.PLAYER_B].Name, stopWatch.Elapsed.TotalSeconds);
                                Console.WriteLine(gazeTargets[(int)Targets.PLAYER_B].Name);
                            }
                            else if (currentTarget != "elsewhere" && !gazeTargets[(int)Targets.PLAYER_A].IsLookingAtTarget(distPlayerA) && !gazeTargets[(int)Targets.PLAYER_B].IsLookingAtTarget(distPlayerB) && !gazeTargets[(int)Targets.MAINSCREEN].IsLookingAtTarget(distMAINSCREEN))
                            {
                                currentTarget = "elsewhere";
                                gPublisher.GazeOpenFace(id, newGA.X, newGA.Y, "elsewhere", stopWatch.Elapsed.TotalSeconds);
                                Console.WriteLine("ELSEWHERE");
                            }
                        }
                    }
                }
            }
        }
    }
}
