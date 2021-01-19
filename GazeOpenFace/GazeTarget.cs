using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GazeOpenFace
{
    class GazeTarget
    {
        public string Name;
        private int numSamples;
        private int distThreshold;
        public bool IsCalibrationStarted;
        public bool IsCalibrationFinished;
        private List<GazeAngle> gazeDirectionGroundTruthSamples;
        private List<HeadPose> headposeGroundTruthSamples;
        Point2D locationFromCam;

        public GazeTarget(string targetName, int samples, int threshold)
        {
            Name = targetName;
            numSamples = samples;
            distThreshold = threshold;
            IsCalibrationStarted = false;
            IsCalibrationFinished = false;
            gazeDirectionGroundTruthSamples = new List<GazeAngle>();
            headposeGroundTruthSamples = new List<HeadPose>();
        }

        public void AddGazeDirSample(GazeAngle ga)
        {
            gazeDirectionGroundTruthSamples.Add(ga);
            if (gazeDirectionGroundTruthSamples.Count == numSamples && headposeGroundTruthSamples.Count == numSamples)
            {
                IsCalibrationFinished = true;
                IsCalibrationStarted = false;
            }
        }

        public void AddHeadPoseSample(HeadPose hp)
        {
            headposeGroundTruthSamples.Add(hp);
            if (gazeDirectionGroundTruthSamples.Count == numSamples && headposeGroundTruthSamples.Count == numSamples)
            {
                IsCalibrationFinished = true;
                IsCalibrationStarted = false;
            }
        }

        public bool IsGazeDirEnoughSamples()
        {
            return gazeDirectionGroundTruthSamples.Count == numSamples;
        }

        public bool IsHeadPoseEnoughSamples()
        {
            return headposeGroundTruthSamples.Count == numSamples;
        }

        public static Point2D ComputeLocationFromCam(GazeAngle ga, HeadPose hp)
        {
            float x = hp.L_Z * (float)Math.Tan(ga.X * Math.PI / 180) + hp.L_X;
            float y = hp.L_Z * (float)Math.Tan(ga.Y * Math.PI / 180) + hp.L_Y;
            return new Point2D(x, y);
        }

        public void ComputeAVGLocationFromCam()
        {
            float sum_x = 0;
            float sum_y = 0;
            for (int i = 0; i < numSamples; i++)
            {
                Point2D p = ComputeLocationFromCam(gazeDirectionGroundTruthSamples[i], headposeGroundTruthSamples[i]);
                sum_x += p.X;
                sum_y += p.Y;
            }
            locationFromCam = new Point2D(sum_x / numSamples, sum_y / numSamples);
        }

        public double DistanceFromPoint(Point2D point)
        {
            return Math.Sqrt(Math.Pow(point.X - locationFromCam.X, 2) + Math.Pow(point.Y - locationFromCam.Y, 2));
        }

        public bool IsLookingAtTarget(Point2D point)
        {
            double dist = Math.Sqrt(Math.Pow(point.X - locationFromCam.X, 2) + Math.Pow(point.Y - locationFromCam.Y, 2));
            return dist <= distThreshold;
        }

        public bool IsLookingAtTarget(double dist)
        {
            return dist <= distThreshold;
        }
    }
}
