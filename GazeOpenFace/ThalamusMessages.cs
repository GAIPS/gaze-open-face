using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thalamus;

namespace GazeOpenFace
{
    public interface IGazeOpenFacePerceptions : IPerception
    {
        void GazeOpenFace(string target);
    }
}
