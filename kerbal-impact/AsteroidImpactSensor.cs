using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kerbal_impact
{
    public class AsteroidImpactSensor : PartModule
    {
        [KSPField] public string experiments = "";

        // -------- tunables (cfg / MM patch) --------
        [KSPField] public float minImpactSpeed = 10f;

        // Global class multipliers (A..E). 1.0 = neutral
        [KSPField] public float classMultA = 1.0f;
        [KSPField] public float classMultB = 1.0f;
        [KSPField] public float classMultC = 1.0f;
        [KSPField] public float classMultD = 1.0f;
        [KSPField] public float classMultE = 1.0f;

        // Per-experiment scalars (apply before class multiplier)
        [KSPField] public float multSeis = 1.0f; // AST-SEIS
        [KSPField] public float multEjecta = 1.0f; // AST-EJECTA
        [KSPField] public float multVolatiles = 1.0f; // AST-VOLATILES
        [KSPField] public float multMagdust = 1.0f; // AST-MAGDUST
        [KSPField] public float multAlbedo = 1.0f; // AST-ALBEDO
        [KSPField] public float multRingdown = 1.0f; // AST-RINGDOWN

        // (Optional) expose experiment IDs so packs can rename them without code changes
        //public string expSeis = "AST-SEIS";
        //public string expVolatiles = "AST-VOLATILES";
        public static string expEjecta = "AST-EJECTA";
        public static string expMagdust = "AST-MAGDUST";
        public static string expAlbedo = "AST-ALBEDO";
        public static string expRingdown = "AST-RINGDOWN";

        // -------------------------------------------
        [KSPField(isPersistant = true)] public bool firedThisFrame = false;

        internal List<string> validExperiments = new List<string>();
        void Start()
        {
            // Parse experiments into list for use later
            if (string.IsNullOrWhiteSpace(experiments))
            {
                return ;
            }

            validExperiments = experiments.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
        }
    }
}
