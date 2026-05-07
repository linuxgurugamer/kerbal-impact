using System.Linq;
using UnityEngine;

namespace kerbal_impact
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidDensityReader : MonoBehaviour
    {
        public static float AsteroidDensity { get; set; }
        public const float StockDensity = 0.03f;
        public static float Multiplier {  get {  return AsteroidDensity / StockDensity; } }    

        public void Start()
        {
            // Find the asteroid prefab ("PotatoRoid") in the loaded parts
            var asteroidPart = PartLoader.getPartInfoByName("PotatoRoid")?.partPrefab;
            if (asteroidPart == null)
            {
                Debug.LogError("[AsteroidTools] Could not find PotatoRoid part!");
                return;
            }

            // Get the ModuleAsteroid from the prefab
            var asteroidModule = asteroidPart.Modules.OfType<ModuleAsteroid>().FirstOrDefault();
            if (asteroidModule == null)
            {
                Debug.LogError("[AsteroidTools] PotatoRoid does not have ModuleAsteroid!");
                return;
            }

            // Access the density field
            AsteroidDensity = asteroidModule.density;
            Debug.Log($"[AsteroidTools] Current asteroid density = {AsteroidDensity}");
        }
    }
}
