using System;
using Thalamus;

namespace GazeOFMessages
{
    public interface IGazeOpenFacePerceptions : IPerception
    {
        void GazeOpenFace(int faceId, double angleX, double angleY, string target, double timeMilisecons);
        void TargetCalibrationStarted(int faceId, string target);
        void TargetCalibrationFinished(int faceId, string target);
        void CalibrationPhaseFinished(int faceId);
    }
}
