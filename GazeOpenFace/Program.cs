using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GazeOpenFace
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                string clientName = args[0];
                int id = int.Parse(args[1]);
                GazeThalamusClient tc = new GazeThalamusClient(clientName, id);
                while (tc.CalibrationPhase) { }
                Console.ReadLine();
                tc.Dispose();
            }
        }
    }
}
