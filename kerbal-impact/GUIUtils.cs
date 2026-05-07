using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace kerbal_impact
{
    public static class GUIUtils
    {
        public static Texture2D pixel;

        public static Camera GetMainCamera()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                return FlightCamera.fetch.mainCamera;
            }
            else
            {
                return Camera.main;
            }
        }
    }
}