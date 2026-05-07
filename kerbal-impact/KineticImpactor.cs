using KSP.Localization;
using System;
using System.Collections;
using UnityEngine;
using static kerbal_impact.ImpactMonitor;


namespace kerbal_impact
{
    internal class KineticImpactor : PartModule
    {
        public bool SetupComplete;
        ScreenMessage Impactmessage = new ScreenMessage("", 7.0f, ScreenMessageStyle.LOWER_CENTER);
        string impactString = "Impactor has impacted";
        float impactVel;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (HighLogic.LoadedSceneIsFlight)
            {
                part.crashTolerance = 999; //to survive rigors of launch
                SetupComplete = true;
                StartCoroutine("delayedStart");
            }
        }

        IEnumerator delayedStart()
        {
            //yield return new WaitForSeconds(0.25f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Log("delayedStart");
            var childColliders = part.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var col in childColliders)
            {
                col.enabled = true;
                col.isTrigger = true;
            }
            part.crashTolerance = 1;
        }

        protected void OnTriggerEnter(Collider col) //science stuff here
        {
            Log("KineticImpactor.OnTriggerEnter");
            Part ImpactedPart = null;
            string vesselName = "";
            try
            {
                ImpactedPart = col.gameObject.GetComponentUpwards<Part>();
                ImpactedPart.Unpack();
            }
            catch (NullReferenceException e)
            {
                LogWarning("Exception thrown in OnColliderEnter: " + e.Message + "\n" + e.StackTrace);
            }
            if (ImpactedPart != null)
            {
                if (this.part.vessel.situation == Vessel.Situations.ORBITING || part.vessel.atmDensity <= 0.001f && this.part.vessel.situation == Vessel.Situations.SUB_ORBITAL)
                {
                    impactVel = (part.vessel.GetObtVelocity() - ImpactedPart.vessel.GetObtVelocity()).magnitude;
                }
                else
                {
                    impactVel = (float)(part.vessel.srf_velocity - ImpactedPart.vessel.srf_velocity).magnitude;
                }
                ModuleAsteroid Ast = ImpactedPart.FindModuleImplementing<ModuleAsteroid>();
                ModuleComet Com = ImpactedPart.FindModuleImplementing<ModuleComet>();
                if (Ast != null)
                {
                    impactString += $" an asteroid";
                    vesselName = ImpactedPart.vessel.vesselName;

                    Log("Asteroid, part.vessel.vesselName: " + part.vessel.vesselName + ", ImpactedPart.vessel.name: " + ImpactedPart.vessel.vesselName  );
                }
                else
                {
                    if (Com != null)
                    {
                        impactString += $" a comet";
                        vesselName = ImpactedPart.vessel.vesselName;
                    }
                    else
                        impactString += $"an innocent {ImpactedPart.partInfo.title}";
                }
                impactString += $" at {impactVel} m/s!";
                if (Ast != null)
                {
#if false
                    ImpactScienceData data = createAsteroidSpectralData(ImpactedPart.vessel.mainBody, Ast, this.vessel, part.flightID);



                    foreach (ProtoPartModuleSnapshot mod in part.protoPartSnapshot.modules)
                    {
                        if (mod.moduleName == "KineticImpactor")
                        {
                            Log("Found KineticImpactor");
                            NewResult(mod.moduleValues, data);
                            break;
                        }
                    }
#endif
                }
            }
            else
            {
                //hitting buildings
                impactVel = (float)vessel.velocityD.magnitude;
                DestructibleBuilding hitBuilding = null;
                try
                {
                    hitBuilding = col.gameObject.GetComponentUpwards<DestructibleBuilding>();
                }
                catch (NullReferenceException e)
                {
                    LogWarning("Exception thrown in OnColliderEnter: " + e.Message + "\n" + e.StackTrace);
                }
                if (hitBuilding != null && hitBuilding.IsIntact)
                {
                    impactString += $" a building at {impactVel} m/s! Oops. Better contact your insurance agent.";
                }
                else
                    impactString += $" a planet {impactVel} m/s!";
            }

            GameEvents.onCollision.Fire(new EventReport(FlightEvents.COLLISION, part,
                vesselName, 
                Localizer.Format("#autoLOC_204427")));


            Impactmessage.textInstance = null;
            Impactmessage.message = impactString.ToString();
            Impactmessage.style = ScreenMessageStyle.UPPER_CENTER;

            ScreenMessages.PostScreenMessage(Impactmessage);

            part.explode();
        }
        protected ImpactScienceData result;

        internal void addExperiment(ImpactScienceData newData)
        {
            //only replace if it is better than any existing results
            if (result == null || newData.dataAmount > result.dataAmount)
            {
                Log("Trying to save impact");
                result = newData;
            }
        }

        internal static void NewResult(ConfigNode node, ImpactScienceData newData)
        {
            Log("KineticImpactor.NewResult, dataAmount: " + newData.dataAmount);

            //only replace if it is better than any existing results
            if (node.HasNode("ScienceData"))
            {
                ConfigNode storedDataNode = node.GetNode("ScienceData");
                ImpactScienceData data = new ImpactScienceData(storedDataNode);
                if (newData.dataAmount <= data.dataAmount)
                {
                    Log("Discarding because better data is already stored");
                    return;
                }
            }
            OnSave(node, newData);
        }

        public override void OnSave(ConfigNode node)
        {
            OnSave(node, result);
        }

        public static void OnSave(ConfigNode node, ImpactScienceData data)
        {
            Log("Saving KineticImpactor");
            DumpNode(node);
            node.RemoveNodes("ScienceData"); //** Prevent duplicates            
            if (data != null)
            {
                ConfigNode storedDataNode = node.AddNode("ScienceData");
                data.SaveImpact(storedDataNode);
            }
        }


#if false
        private static ImpactScienceData createAsteroidSpectralData(CelestialBody crashBody, ModuleAsteroid Ast, Vessel crashVessel, uint flightID)
        {
            Vessel asteroid = Ast.vessel;

            double crashVelocity = Math.Abs(crashVessel.srf_velocity.magnitude - Ast.vessel.srf_velocity.magnitude);
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            //double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("AsteroidDensity");
            ExperimentSituations situation = ScienceUtil.GetExperimentSituation(asteroid);

            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, situation, asteroid.id.ToString(), asteroid.GetName(), crashBody, "", "");
            double science = subject.scienceCap;
            Log("Impact took place in " + situation);
            String flavourText = "Impact at <<1>> on <<2>>";


            var sTov = SpeedToValue((float)crashVelocity, crashMasss);

            Log("SpeedtoValue = " + sTov.ToString("F3"));
            Log("crashMasss = " + crashMasss.ToString("F1"));

            science /= subject.subjectValue * sTov;
            Log("subject: " + subject.title + ", subjectValue: " + subject.subjectValue);
            Log("science: " + science.ToString("F1"));
            ImpactScienceData data = new ImpactScienceData(0,
                                                            asteroid.GetName(),
                                                            (float)(science * subject.dataScale),
                                                            1f,
                                                            0,
                                                            subject.id,
                                                            Localizer.Format(flavourText, asteroid.GetName(), crashBody.GetDisplayName()),
                                                            false,
                                                            flightID);

            ScreenMessages.PostScreenMessage(
                Localizer.Format("#autoLOC_AstScience_Density_default", asteroid.GetName(), crashBody.GetDisplayName()),
                15.0f, ScreenMessageStyle.UPPER_RIGHT);

            return data;
        }
#endif

        // Approximate KSP1 asteroid mass range in metric tons (A–E) for stock

        const float MinAsteroidMass = 10f;     // ~Class A lower bound
        const float MaxAsteroidMass = 4000f;  // ~Class E upper bound
        const float optimalLowImpactSpeed = 25f;
        const float optimalHighImpactSpeed = 100f;

        static float MinAstAdjMass { get { return MinAsteroidMass * AsteroidDensityReader.Multiplier; } }
        static float MaxAstAdjMass { get { return MaxAsteroidMass * AsteroidDensityReader.Multiplier; } }


        /// <summary>
        /// Returns [0..1]. Peaks at speed=25 and speed=100, with ~0.5 valley at 62.5.
        /// Output is then scaled by a logarithmic mass factor (smaller mass => higher value).
        /// </summary>
        public static float SpeedToValue(float speed, float massTons, float maxAsteroidMass = MaxAsteroidMass)
        {
            Log("SpeedToValue, speed: " + speed + ", massTons: " + massTons + ", maxAsteroidMass: " +  maxAsteroidMass);
            // --- Base curve with two peaks (speed only) ---
            float baseVal;
            if (speed <= 0f)
            {
                baseVal = 0f;
            }
            else if (speed < 25f)
            {
                // Raised-cosine ramp: 0 at 0  ->  1 at 25
                baseVal = 0.5f * (1f - Mathf.Cos(Mathf.PI * (speed / 25f)));
            }
            else if (speed <= 100f)
            {
                // Two half-cosine lobes over [25,100], valley ~0.5 at 62.5
                float t = (speed - 25f) / 75f; // maps 25..100 -> 0..1
                float lobe25 = 0.5f * (1f + Mathf.Cos(Mathf.PI * t));       // 1 at 25 -> 0 at 100
                float lobe100 = 0.5f * (1f + Mathf.Cos(Mathf.PI * (1f - t))); // 0 at 25 -> 1 at 100
                baseVal = Mathf.Max(lobe25, lobe100); // valley ~0.5 at midpoint
            }
            else if (speed <= 200f)
            {
                // Smooth half-cosine tail: 1 at 100 -> 0 at 200
                float u = (speed - 100f) / 100f;           // 100..200 -> 0..1
                baseVal = 0.5f * (1f + Mathf.Cos(Mathf.PI * u));
            }
            else
            {
                baseVal = 0f; // keep the original "above 100 -> 0" behavior
            }
            Log("baseVal: " + baseVal);

            if (baseVal <= 0f) return 0f;



            // --- Logarithmic mass scaling (smaller mass => larger factor) ---
            float minM = Mathf.Max(MinAstAdjMass, 0.001f);
            float maxM = Mathf.Max(MaxAstAdjMass, minM + 1f);
            float m = Mathf.Clamp(massTons, minM, maxM);

            Log("m: " + m);
            // factor = log(max/m) / log(max/min)  ->  1 at min mass, 0 at max mass
            float massFactor = Mathf.Log(maxM / m) / Mathf.Log(maxM / minM);
            Log("massFactor: " + massFactor);

            return Mathf.Clamp01(baseVal * massFactor);
        }
    }
}