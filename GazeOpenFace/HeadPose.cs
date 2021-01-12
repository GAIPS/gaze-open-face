using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GazeOpenFace
{
    class HeadPose
    {
        public float L_X;
        public float L_Y;
        public float L_Z;
        public float R_X;
        public float R_Y;
        public float R_Z;

        public HeadPose(float lx, float ly, float lz, float rx, float ry, float rz)
        {
            L_X = lx;
            L_Y = ly;
            L_Z = lz;
            R_X = rx;
            R_Y = ry;
            R_Z = rz;
        }
    }
}
