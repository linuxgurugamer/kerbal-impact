using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace kerbal_impact
{

    public class ImpactMonitor //: MonoBehaviour
    {

        internal static ImpactMonitor instance;

        public Vessel LastActiveLauncher = null;

        private ImpactMonitor()
        {
        }

        public static ImpactMonitor getInstance()
        {
            if (instance == null)
            {
                instance = new ImpactMonitor();
                Log("Starting from getInsance");
            }
            return instance;
        }

        #region Logging
        public static void Log(string message)
        {
            Debug.Log("[KerbalImpact:" + DateTime.Now + "]: " + message);
        }
        public static void LogError(string message)
        {
            Debug.Log("[KerbalImpact:" + DateTime.Now + "] ERROR: " + message);
        }

        public static void LogWarning(string message)
        {
            Debug.Log("[KerbalImpact:" + DateTime.Now + "] WARNING: " + message);
        }

        public static void DumpNode(ConfigNode node, string header = null)
        {
            Log("DumpNode");
            if (node == null)
            {
                Debug.Log("[KerbalImpact] Node is null");
                return;
            }

            if (!string.IsNullOrEmpty(header))
                Debug.Log($"[KerbalImpact] Dumping ConfigNode: {header}");

            // ConfigNode has a ToString(int indent) overload
            Debug.Log(node.ToString());

            if (!string.IsNullOrEmpty(header))
                Debug.Log($"[KerbalImpact] End of {header}");
        }
        #endregion

        public void Start()
        {
            Log("Its starting");
            GameEvents.onCrash.Add(OnCrash);
            GameEvents.onCollision.Add(OnCollide);
            GameEvents.OnVesselRecoveryRequested.Add(OnVesselRecovered);

            //listBiones(Planetarium.fetch.Sun);
        }

        private void listBiomes(CelestialBody body)
        {
            //todo temporary
            Log("attname=" + body.bodyName);
            CBAttributeMapSO m = body.BiomeMap;
            CBAttributeMapSO.MapAttribute[] atts = m.Attributes;
            foreach (CBAttributeMapSO.MapAttribute att in atts)
            {
                Log("att=" + att.name + "-" + att.value);
            }
            foreach (CelestialBody sub in body.orbitingBodies)
            {
                listBiomes(sub);
            }
        }

        public void Stop()
        {
            GameEvents.onCrash.Remove(OnCrash);
            GameEvents.onCollision.Remove(OnCollide);
            GameEvents.OnVesselRecoveryRequested.Remove(OnVesselRecovered);

        }

        private void OnVesselRecovered(Vessel vessel)
        {
            List<Seismometer> seismographs = vessel.FindPartModulesImplementing<Seismometer>();
            IEnumerable<ImpactScienceData> sciences = seismographs.SelectMany(s => s.GetImpactData());
            //TODO add spectrograph data to this too
            foreach (ImpactScienceData science in sciences)
            {
                scienceToKSC(science);
            }
        }

        public void scienceToKSC(ImpactScienceData data)
        {
            ImpactCoordinator.getInstance().scienceListeners.Fire(data);
        }

        private void OnCrash(EventReport report)
        {
            Part crashPart = report.origin;
            if (crashPart.vessel.srf_velocity.magnitude < 50) return;
            Log("crash data " + report.msg + "-" + report.eventType + "-" + report.other + "- " + report.sender + "-" + crashPart.vessel + "-" + crashPart.vessel.srf_velocity.magnitude);
            Vessel crashVessel = crashPart.vessel;
            doImpact(crashVessel, null);
        }

        private void OnCollide(EventReport report)
        {
            Part crashPart = report.origin;
            Log("Something crashed into something: " + crashPart + "->" + report.other);
            if (crashPart.vessel.srf_velocity.magnitude < 50) return;
            Vessel asteroid = null;
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if ((v.vesselName == report.other || v.vesselName == report.sender)
                    && v.vesselType == VesselType.SpaceObject)
                {
                    asteroid = v;
                    break;
                }
            }
            if (report.other != "the surface" && asteroid == null) return;
            Log("collide data " + report.msg + "-" + report.eventType + "-" + report.other + "- " + report.sender + "-" + crashPart.vessel + "-" + crashPart.vessel.srf_velocity.magnitude);
            Vessel crashVessel = crashPart.vessel;
            doImpact(crashVessel, asteroid);
        }

        // Following to be used when adding direct support for comets
#if false
        public static bool IsAsteroid(Vessel vessel)
        {
            if (vessel == null) return false;
            foreach (Part p in vessel.parts)
            {
                if (p.FindModuleImplementing<ModuleAsteroid>() != null)
                    return true;
            }
            return false;
        }

        public static bool IsComet(Vessel vessel)
        {
            if (vessel == null) return false;
            foreach (Part p in vessel.parts)
            {
                if (p.FindModuleImplementing<ModuleComet>() != null)
                    return true;
            }
            return false;
        }
#endif
        private void doImpact(Vessel crashVessel, Vessel asteroid)
        {
            CelestialBody crashBody = crashVessel.orbit.referenceBody;
            if (crashBody.atmosphere && asteroid == null) return;
            Log("Crashed on " + crashBody.name);

            Part part = crashVessel.Parts[0];


            //find all craft orbiting and landed at this body
            foreach (Vessel vessel in FlightGlobals.Vessels.Where(v => v.orbit.referenceBody == crashBody))
            {
                if (vessel.id != crashVessel.id)
                {
                    Log("Found a vessel " + vessel.GetName());
                    if (asteroid == null)
                    {
                        if (vessel.situation == Vessel.Situations.LANDED)
                        {
                            landedVessel(crashBody, vessel, crashVessel);
                        }
                        if (vessel.situation == Vessel.Situations.ORBITING)
                        {
                            orbitingVessel(crashBody, vessel, crashVessel);
                        }
                    }
                    else
                    {
                        if (vessel.situation == Vessel.Situations.ORBITING)
                        {
                            ReturnToObserver.instance.Initiate(LastActiveLauncher);
                            nearAsteroidVessel(vessel, crashVessel, asteroid, crashBody);
                        }
                    }
                }
            }
        }

        private void landedVessel(CelestialBody crashBody, Vessel vessel, Vessel crashVessel)
        {
            Log("And it is landed");
            if (vessel.loaded)
            {
                List<Seismometer> seismographs = vessel.FindPartModulesImplementing<Seismometer>();
                if (seismographs.Count != 0)
                {
                    Log("Found seismographs");
                    ImpactScienceData data = createSeismicData(crashBody, crashVessel, seismographs[0].part.flightID);
                    ImpactCoordinator.getInstance().bangListeners.Fire(data);
                    seismographs[0].addExperiment(data);
                }
            }
            else
            {
                List<ProtoPartSnapshot> parts = vessel.protoVessel.protoPartSnapshots;
                foreach (ProtoPartSnapshot snap in parts)
                {
                    foreach (ProtoPartModuleSnapshot mod in snap.modules)
                    {
                        if (mod.moduleName == "Seismometer")
                        {
                            Log("Found seismographs");
                            ImpactScienceData data = createSeismicData(crashBody, crashVessel, snap.flightID);
                            ImpactCoordinator.getInstance().bangListeners.Fire(data);
                            Seismometer.NewResult(mod.moduleValues, data);
                            return;
                        }
                    }
                }
            }
        }


        private void nearAsteroidVessel(Vessel observer, Vessel crashVessel, Vessel asteroid, CelestialBody crashBody)
        {
            Log("ImpactMonitor.nearAsteroidVessel");
            Log("observer is orbiting ");
            Log("observer is at " + observer.CoM);
            Log("Crash vessel is at" + crashVessel.CoM);
            Vector3d sightVec = observer.CoM - crashVessel.CoM;
            Log("Distance between them: " + sightVec.magnitude);


            if (sightVec.magnitude < 5e5)
            {
                //observer is in range (500km)
                Log("It is in range: " + (sightVec).magnitude);
                if (observer.loaded)
                {
                    Spectrometer spectrometer = observer.FindPartModuleImplementing<Spectrometer>();
                    if (spectrometer != null)
                    {
                        Log("Found loaded spectrometers");
                        ImpactScienceData data = createAsteroidSpectralData(crashBody, asteroid, crashVessel, spectrometer.part.flightID);
                        ImpactCoordinator.getInstance().bangListeners.Fire(data);
                        spectrometer.addExperiment(data);
                    }

                    Densimeter densimeter = observer.FindPartModuleImplementing<Densimeter>();
                    if (densimeter != null)
                    {
                        Log("Found loaded densimeters");
                        if (densimeter.observerPartModule.deployed || !densimeter.observerPartModule.deployable)
                        {
                            ImpactScienceData data = createAsteroidDensityData(crashBody, asteroid, crashVessel, densimeter.part.flightID);
                            Log("asteroid: " + asteroid.vesselName + ", crashVessel: " + crashVessel.vesselName);
                            ImpactCoordinator.getInstance().bangListeners.Fire(data);
                            densimeter.addExperiment(data);
                            //crashVessel.OnDestroy();

                        }
                    }

                    AsteroidImpactSensor impactSensors = observer.FindPartModuleImplementing<AsteroidImpactSensor>();
                    if (impactSensors != null)
                    {
                        Log("Found loaded impactSensors");
                        if (densimeter.observerPartModule.deployed || !densimeter.observerPartModule.deployable)
                        {
                            RunObserverExperiments(densimeter.observerPartModule.part, crashBody, asteroid, crashVessel, impactSensors.part.flightID, impactSensors);
                        }
                    }
                }
                else
                {
                    Log($"Unloaded vessel: {observer.vesselName}");
                    List<ProtoPartSnapshot> parts = observer.protoVessel.protoPartSnapshots;
                    ProtoPartModuleSnapshot impactSensor = null;
                    ProtoPartSnapshot part = null;

                    foreach (ProtoPartSnapshot snap in parts)
                    {
                        Log($"   Unloaded part: {snap.partName}  persistentId: {snap.persistentId}");
                        foreach (ProtoPartModuleSnapshot mod in snap.modules)
                        {
                            if (mod.moduleName == "Spectrometer")
                            {
                                Log("Found unloaded spectrometers");
                                ImpactScienceData data = createAsteroidSpectralData(crashBody, asteroid, crashVessel, snap.flightID);
                                ImpactCoordinator.getInstance().bangListeners.Fire(data);
                                Spectrometer.NewResult(mod.moduleValues, data);
                                continue;
                            }
                            if (mod.moduleName == "Densimeter")
                            {
                                Log("Found unloaded Densimeter");
                                ImpactScienceData data = createAsteroidDensityData(crashBody, asteroid, crashVessel, snap.flightID);
                                ImpactCoordinator.getInstance().bangListeners.Fire(data);
                                Densimeter.NewResult(mod.moduleValues, data);
                                continue;
                            }
                            if (mod.moduleName == "AsteroidImpactSensor")
                            {
                                Log("   Found unloaded AsteroidImpactSensor");
                                part = snap;
                                impactSensor = mod;

                                // TODO
                                // Need to figure out the observerPartModule, probably won't work the way it's set up
                                // May ned to search for the Spectrometer and check it here
                                bool deployable = false;
                                bool deployed = false;

                                ConfigNode moduleScienceContainer = null;
                                foreach (ProtoPartModuleSnapshot m in snap.modules)
                                {
                                    Log("moduleName: " + m.moduleName);
                                    if (m.moduleName == "ModuleScienceContainer")
                                    {
                                        Log("Dumping ModuleScienceContainer");
                                        DumpNode(m.moduleValues);
                                        moduleScienceContainer = m.moduleValues;
                                    }
                                    if (m.moduleName == "Densimeter")
                                    {
                                        if (m.moduleValues.TryGetValue("deployed", ref deployable))
                                        {
                                            m.moduleValues.TryGetValue("deployable", ref deployed);
                                        }
                                    }
                                }
                                Log($"deployable: {deployable}   deployed: {deployed}");
                                /*
                                ScienceData
                                {
                                    data = 15.377018
                                    scienceValueRatio = 1
                                    subjectID = AST - EJECTA@SunInSpaceLowAsteroidC
                                    xmit = 0.600000024
                                    xmitBonus = 0.899999976
                                    title = Regolith Ejecta Profiling from space just above The Sun's AsteroidC
                                    triggered = False
                                    container = 3656826559
                                }
                                */
                                //ScienceData
                                //ModuleScienceContainer
                                ConfigNode scienceNode = new ConfigNode("ScienceData");


                                scienceNode.AddValue("container", part.flightID);

                                //moduleScienceContainer.AddNode(node);
                                //Log("Dumping ModuleScienceContainer");
                                //DumpNode(science);

                                if (deployed || !deployable)
                                {
                                    if (moduleScienceContainer != null)
                                        RunObserverExperiments(snap.partPrefab, crashBody, asteroid, crashVessel, snap.flightID, moduleScienceContainer, scienceNode);
                                    else
                                    {
                                        ScreenMessages.PostScreenMessage("[Impact] No ScienceContainer on part to store data!", 5f, ScreenMessageStyle.UPPER_CENTER);
                                        Log("[Impact] No ScienceContainer on part to store data!");
                                    }

                                }

                                continue;
                            }

                        }
                    }
                }


            }
        }

        private bool IsInSituation(Vessel observer, ExperimentSituations situationMask)
        {
            ExperimentSituations i = ScienceUtil.GetExperimentSituation(observer);
            return ((int)i & (int)situationMask) > 0;
        }

        private void orbitingVessel(CelestialBody crashBody, Vessel observer, Vessel crashVessel)
        {
            Log("And it is orbiting");
            Log("CelestialBody is at " + crashBody.position);
            Log("Crash vessel is at" + crashVessel.CoM);
            Log("Observer is at" + observer.CoM);
            Vector3d crash = crashVessel.CoM;
            crash = crashVessel.CoM - crashBody.position;
            Log("crashRelaticeTocentre =" + crash);
            Vector3d orbVec = observer.CoM - crashBody.position;
            Vector3d sightVec = (orbVec - crash);
            double angle = Vector3d.Angle(crash, sightVec);
            Log("Sight=" + sightVec);
            Log("sight angle = " + angle + " degrees");
            Log("Distance between them =" + sightVec.magnitude);

            if (angle < 90)
            {
                Log("Vessel is visible");
                if (observer.loaded)
                {
                    Log("Vessel is loaded");

                    List<Spectrometer> spectrometers = observer.FindPartModulesImplementing<Spectrometer>();
                    if (spectrometers.Count != 0)
                    {
                        foreach (var s in spectrometers)
                        {
                            if (s != null)
                            {
                                Log("vessel: " + observer.name + ", situationMask: " + s.situationMask);
                                if ((s.deployable && s.deployed) || !s.deployable)
                                {
                                    if (IsInSituation(observer, (ExperimentSituations)s.situationMask))
                                    {
                                        ImpactScienceData data = createSpectralData(crashBody, crashVessel, s.part.flightID, s.situationMask);
                                        ImpactCoordinator.getInstance().bangListeners.Fire(data);
                                        s.addExperiment(data);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Log("Vessel is unloaded");
                    List<ProtoPartSnapshot> parts = observer.protoVessel.protoPartSnapshots;
                    foreach (ProtoPartSnapshot snap in parts)
                    {
                        foreach (ProtoPartModuleSnapshot mod in snap.modules)
                        {
                            if (mod.moduleName == "Spectrometer")
                            {
                                Log("Found spectrometer, part: " + snap.partName);

                                bool deployable = false;
                                bool deployed = false;
                                int situationMask = 0;

                                if (mod.moduleValues.HasValue("deployable"))
                                    deployable = bool.Parse(mod.moduleValues.GetValue("deployable"));
                                if (mod.moduleValues.HasValue("deployed"))
                                    deployed = bool.Parse(mod.moduleValues.GetValue("deployed"));
                                if (mod.moduleValues.HasValue("situationMask"))
                                    situationMask = int.Parse(mod.moduleValues.GetValue("situationMask"));

                                Log("vessel: " + observer.name + ", situationMask: " + situationMask + ", is deployable: " + deployable + ", is deployed: " + deployed);
                                if ((deployable && deployed) || !deployable)
                                {
                                    if (IsInSituation(observer, (ExperimentSituations)situationMask))
                                    {
                                        Log("Found spectrometers, in good situation");
                                        ImpactScienceData data = createSpectralData(crashBody, crashVessel, snap.flightID, situationMask);
                                        Log("about to call listeners");
                                        ImpactCoordinator.getInstance().bangListeners.Fire(data);
                                        Log("About to call newresult");
                                        Spectrometer.NewResult(mod.moduleValues, data);
                                        continue;
                                    }
                                }
                            }

#if false

                            if (mod.moduleName == "Densimeter")
                            {
                                Log("Found Densimeter, part: " + snap.partName);

                                bool deployable = false;
                                bool deployed = false;
                                int situationMask = 0;

                                if (mod.moduleValues.HasValue("deployable"))
                                    deployable = bool.Parse(mod.moduleValues.GetValue("deployable"));
                                if (mod.moduleValues.HasValue("deployed"))
                                    deployed = bool.Parse(mod.moduleValues.GetValue("deployed"));
                                if (mod.moduleValues.HasValue("situationMask"))
                                    situationMask = int.Parse(mod.moduleValues.GetValue("situationMask"));

                                Log("vessel: " + observer.name + ", situationMask: " + situationMask + ", is deployable: " + deployable + ", is deployed: " + deployed);
                                if ((deployable && deployed) || !deployable)
                                {
                                    if (IsInSituation(observer, (ExperimentSituations)situationMask))
                                    {
                                        Log("Found spectrometers, in good situation");
                                        ImpactScienceData data = createSpectralData(crashBody, crashVessel, snap.flightID, situationMask);
                                        Log("about to call listeners");
                                        ImpactCoordinator.getInstance().bangListeners.Fire(data);
                                        Log("About to call newresult");
                                        Spectrometer.NewResult(mod.moduleValues, data);
                                        continue;
                                    }
                                }
                            }

#endif




                        }
                    }
                }
            }
        }


        private static ImpactScienceData createSeismicData(CelestialBody crashBody, Vessel crashVessel, uint flightID)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash


            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSeismometer");
            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.SrfLanded, crashBody, "", "");
            double science = translateKEToScience(crashEnergy, crashBody, subject);

            String flavourText = "Impact of <<1>> on <<2>>";
            Log(" caluculated science =" + science);
            science = Math.Max(0.01, science - subject.science);
            Log("residual science =" + science);

            science /= subject.subjectValue;
            Log("divided science =" + science);

            ImpactScienceData data = new ImpactScienceData(ImpactScienceData.DataTypes.Seismic,
                (float)crashEnergy, null, crashVessel.latitude,
                (float)(science * subject.dataScale), 1f, 0, subject.id,
                Localizer.Format(flavourText, energyFormat(crashEnergy), crashBody.GetDisplayName()), false, flightID, ExperimentSituations.SrfLanded);

            ScreenMessages.PostScreenMessage(
                Localizer.Format("#autoLOC_Screen_Seismic", energyFormat(crashEnergy), crashBody.GetDisplayName()),
                5.0f, ScreenMessageStyle.UPPER_RIGHT);


            return data;
        }

        private static ImpactScienceData createSpectralData(CelestialBody crashBody, Vessel crashVessel, uint flightID, int situationMask)
        {
            double crashVelocity = crashVessel.srf_velocity.magnitude;
            Log("Velocity=" + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            //double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("ImpactSpectrometer");
            String biome = ScienceUtil.GetExperimentBiome(crashBody, crashVessel.latitude, crashVessel.longitude);
            CBAttributeMapSO m = crashBody.BiomeMap;
            CBAttributeMapSO.MapAttribute[] atts = m.Attributes;
            //ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.InSpaceLow, crashBody, biome, biome);
            ScienceSubject subject = null;

            if ((situationMask & (int)ExperimentSituations.InSpaceLow) != 0)
                subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.InSpaceLow, crashBody, biome, biome);
            if ((situationMask & (int)ExperimentSituations.InSpaceHigh) != 0)
                subject = ResearchAndDevelopment.GetExperimentSubject(experiment, ExperimentSituations.InSpaceHigh, crashBody, biome, biome);

            if (subject != null)
            {
                double science = subject.scienceCap;
                Log("Impact took place in " + biome + " at " + crashVessel.latitude + "," + crashVessel.longitude);
                String flavourText = "Impact at <<1>> on <<2>>";

                science = Math.Max(0, science - subject.science);
                science /= subject.subjectValue;

                Log("subject.science: " + subject.science + ", subject.subjectValue: " + subject.subjectValue + ", science: " + science);
                ImpactScienceData data = new ImpactScienceData(ImpactScienceData.DataTypes.Spectral,
                    0, biome, crashVessel.latitude,
                    (float)(science * subject.dataScale), 1f, 0, subject.id,
                    Localizer.Format(flavourText, biome, crashBody.GetDisplayName()), false, flightID, (ExperimentSituations)situationMask);

                ScreenMessages.PostScreenMessage(
                    Localizer.Format("#autoLOC_Screen_Spectrum", biome, crashBody.GetDisplayName()),
                    5.0f, ScreenMessageStyle.UPPER_RIGHT);
                return data;
            }
            return null;
        }

        private static ImpactScienceData createAsteroidSpectralData(CelestialBody crashBody, Vessel asteroid, Vessel crashVessel, uint flightID)
        {
            //double crashVelocity = crashVessel.srf_velocity.magnitude;
            double crashVelocity = Math.Abs(crashVessel.srf_velocity.magnitude - asteroid.srf_velocity.magnitude);

            Log("Velocity: " + crashVelocity);
            float crashMasss = crashVessel.GetTotalMass() * 1000;
            //double crashEnergy = 0.5 * crashMasss * crashVelocity * crashVelocity; //KE of crash

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("AsteroidSpectometry");
            ExperimentSituations situation = ScienceUtil.GetExperimentSituation(asteroid);

            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, situation, asteroid.id.ToString(), asteroid.GetName(), crashBody, "", "");
            double science = subject.scienceCap;
            Log("Impact took place in " + situation);
            String flavourText = "Impact at <<1>> on <<2>>";

            science /= subject.subjectValue;

            Log("science: " + science);
            Log("subject.id: " + subject.id);
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
                Localizer.Format("#autoLOC_Screen_Asteroid", asteroid.GetName(), crashBody.GetDisplayName()),
                5.0f, ScreenMessageStyle.UPPER_RIGHT);

            return data;
        }

        private static ImpactScienceData createAsteroidDensityData(CelestialBody crashBody, Vessel asteroid, Vessel crashVessel, uint flightID)
        {
            //KineticImpactor kineticImpactor = crashVessel.FindPartModuleImplementing<KineticImpactor>();

            double crashVelocity = Math.Abs(crashVessel.srf_velocity.magnitude - asteroid.srf_velocity.magnitude);

            Log("createAsteroidDensityData, Velocity: " + crashVelocity);
            float crashMass = crashVessel.GetTotalMass() * 1000;

            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment("AsteroidDensity");
            ExperimentSituations situation = ScienceUtil.GetExperimentSituation(asteroid);

            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(experiment, situation, asteroid.id.ToString(), asteroid.GetName(), crashBody, "", "");
            double science = subject.scienceCap;
            Log("Impact took place in " + situation);
            String flavourText = "Impact at <<1>> on <<2>>";

            var sTov = KineticImpactor.SpeedToValue((float)crashVelocity, crashMass);

            Log("SpeedtoValue: " + sTov.ToString("F3"));
            Log("crashMass: " + crashMass.ToString("F1"));

            science /= subject.subjectValue * sTov;

            science = 61;

            Log("subject: " + subject.title + ", subjectValue: " + subject.subjectValue);
            Log("science: " + science.ToString("F1"));
            Log("subject.id: " + subject.id);
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

        public static double translateScienceToKE(double science, CelestialBody crashBody, ScienceSubject subject)
        {
            double referenceEnergy = getReferenceCrash(crashBody);
            Log("ReferenceCrash=" + referenceEnergy);
            double relativeScience = science / subject.scienceCap;
            Log("Science=" + science + " relative = " + relativeScience);
            double crashEnergy = relativeScience * relativeScience * referenceEnergy;
            Log("crashEnergy=" + crashEnergy);
            return crashEnergy;
        }

        public static double translateKEToScience(double crashEnergy, CelestialBody crashBody, ScienceSubject subject)
        {
            double referenceEnergy = getReferenceCrash(crashBody);

            float relativeScience = Math.Min((float)(Math.Sqrt(crashEnergy / referenceEnergy)), 1);
            return relativeScience * subject.scienceCap;
        }

        public static double getReferenceCrash(CelestialBody crashBody)
        {
            //Science amount is relative to a 15 tonne impactor at escape velocity
            double mu = crashBody.gravParameter;
            double radius = crashBody.Radius;
            double referenceEnergy = 15e3 * mu / radius;
            return referenceEnergy;
        }

        private static string[] suffixes = { "J", "kJ", "MJ", "GJ", "TJ", "PJ" };
        public static string energyFormat(double crashEnergy)
        {
            int suffixIndex = 0;
            double energyFigs = crashEnergy;

            while (energyFigs >= 1000 && suffixIndex < suffixes.Count())
            {
                energyFigs /= 1000;
                suffixIndex++;
            }

            //There must be a nice way to show exactly 3 sig figs right?
            string sigFigFormat;
            if (energyFigs >= 100) sigFigFormat = "000";
            else if (energyFigs >= 10) sigFigFormat = "00.0";
            else sigFigFormat = "0.00";
            string result = String.Format("{0:" + sigFigFormat + "}{1}", energyFigs, suffixes[suffixIndex]);
            return result;
        }

        // *********************************************************************************************************************
        // -------- tunables (cfg / MM patch) --------
        [KSPField] public float minImpactSpeed = 10f;


        public bool firedThisFrame = false;


        #region LoadedObserver
        private void RunObserverExperiments(Part observerPart, CelestialBody crashBody, Vessel asteroid, Vessel crashVessel, uint flightID, AsteroidImpactSensor impactSensors)
        {
            Log("RunObserverExperiments");
            if (!HighLogic.LoadedSceneIsFlight)
            {
                Log("Not in flight");
                return;
            }
            if (firedThisFrame)
            {
                Log("firedThisFrame");
                return;
            }

            Part otherPart = asteroid.Parts[0];
            Part part = crashVessel.Parts[0];
            Log("asteroid: " + asteroid.vesselName + ", crashVessel: " + crashVessel.vesselName);

            if (otherPart == null)
            {
                Log("otherPart is null");
                return;
            }

            bool hitAsteroid = otherPart.vessel != null &&
                               otherPart.vessel.parts.Any(p => p.FindModuleImplementing<ModuleAsteroid>() != null);
            if (!hitAsteroid)
            {
                Log("Did not hit asteroid");
                return;
            }
            double crashVelocity = Math.Abs(crashVessel.srf_velocity.magnitude - asteroid.srf_velocity.magnitude);

            double impactSpeed = crashVelocity;
            if (impactSpeed < minImpactSpeed)
            {
                Log("impactSpeed: " + impactSpeed + ", minImpactSpeed: " + minImpactSpeed);
                return;
            }

            Part asteroidPart = otherPart.vessel.parts.FirstOrDefault(p => p.FindModuleImplementing<ModuleAsteroid>() != null);
            double asteroidMass = asteroidPart != null ? asteroidPart.mass : 0;
            string asteroidClass = GuessAsteroidClassFromMass(asteroidMass);

            FireExperiments(observerPart, impactSpeed, asteroidMass, asteroidClass, impactSensors);

            firedThisFrame = true;
            part.StartCoroutine(ResetFlagNextFrame());
        }

        private System.Collections.IEnumerator ResetFlagNextFrame()
        {
            yield return new WaitForFixedUpdate();
            firedThisFrame = false;
        }

        private string GuessAsteroidClassFromMass(double mass)
        {
            // Simple heuristic; feel free to swap for your pack’s actual mapping
            if (mass < 15) return "A";
            if (mass < 75) return "B";
            if (mass < 300) return "C";
            if (mass < 1000) return "D";
            return "E";
        }

        internal static bool IsSun(CelestialBody body)
        {
            return body.referenceBody == body;
        }
        AsteroidImpactSensor ais = null;

        private void FireExperiments(Part part, double vImpact, double mAst, string aClass, AsteroidImpactSensor impactSensors)
        {
            Log("FireExperiments");
            ais = part.FindModuleImplementing<AsteroidImpactSensor>();
            if (ais == null)
            {
                Log("No AsteroidImpactSensor found on part: " + part.name);
                return;
            }
            Log("FireExperiments");
            // keep your sweet-spot curves, then apply cfg multipliers inside TryRun()
            //TryRun(part, expSeis, vImpact, aClass, ScoreBell(vImpact, 25, 15) + ScoreBell(vImpact, 100, 30), multSeis, impactSensors);
            //TryRun(part, expVolatiles, vImpact, aClass, ScoreBell(vImpact, 80, 25) * (aClass == "C" ? 1.3f : 1.0f), multVolatiles, impactSensors);

            TryRun(part, AsteroidImpactSensor.expEjecta, vImpact, aClass, Plateau(vImpact, 25, 200) * (1.0f + Mathf.Clamp01((float)(mAst / 300.0))), ais.multEjecta, impactSensors);
            TryRun(part, AsteroidImpactSensor.expMagdust, vImpact, aClass, ScoreBell(vImpact, 60, 22) * (aClass == "E" ? 1.25f : 1.0f), ais.multMagdust, impactSensors);
            TryRun(part, AsteroidImpactSensor.expAlbedo, vImpact, aClass, Mathf.Clamp01((float)((vImpact - 10.0) / 60.0)), ais.multAlbedo, impactSensors);

            // Special exception, this must have a seismometer on the surface
            TryRun(part, AsteroidImpactSensor.expRingdown, vImpact, aClass, ScoreBell(vImpact, 40, 18), ais.multRingdown, impactSensors);
        }

        [KSPField] public float xmitScalar = 0.6f; // how efficient transmission is
        [KSPField] public float labScalar = 0.9f; // Mobile Lab processing boost

        private void TryRun(Part part, string expID, double vImpact, string aClass, float quality, float expScalar, AsteroidImpactSensor impactSensors)
        {
            Log("TryRun, expID: " + expID + ", vImpact: " + vImpact + ", aClass: " + aClass + ", quality: " + quality + ", expScaler: " + expScalar);
            if (!impactSensors.validExperiments.Contains(expID))
            {
                Log($"TryRun, can't find expID: {expID}");
                return;
            }
            if (quality <= 0.05f)
            {
                Log("TryRun, Quality < 0.05f");
                return;
            }

            ScienceExperiment exp = ResearchAndDevelopment.GetExperiment(expID);
            if (exp == null)
            {
                ScreenMessages.PostScreenMessage("[Impact] Experiment " + expID + " not found", 4f, ScreenMessageStyle.UPPER_CENTER);
                Log("Experiment " + expID + " not found");
                return;
            }

            // Stock asteroid subjects are archived under the Sun
            CelestialBody sun = null;

            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                if (IsSun(FlightGlobals.Bodies[i]))
                {
                    sun = FlightGlobals.Bodies[i];
                    break;
                }
            }
            if (sun == null && Planetarium.fetch != null) sun = Planetarium.fetch.Sun;

            string biome = "Asteroid " + aClass;
            ExperimentSituations situation = ExperimentSituations.InSpaceLow;

            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(exp, situation, sun, biome, string.Empty);
            if (subject == null)
            {
                ScreenMessages.PostScreenMessage("[Impact] Could not create subject for " + expID, 4f, ScreenMessageStyle.UPPER_CENTER);
                Log("[Impact] Could not create subject for " + expID);
                return;
            }

            // Apply: curve -> per-experiment -> class
            float classScalar = GetClassScalar(aClass);
            float scaledQuality = Mathf.Max(0.01f, quality) * Mathf.Max(0.01f, expScalar) * Mathf.Max(0.01f, classScalar);

            // KSP1: dataAmount is first arg; cap with experiment limits, then scale by dataScale
            float dataAmount = Mathf.Clamp(scaledQuality * exp.baseValue, 0.1f, exp.scienceCap) * exp.dataScale;

            // KSP1 ctor: ScienceData(float dataAmount, float xmitScalar, float labScalar, string subjectID, string title)
            ScienceData data = new ScienceData(
                dataAmount,
                xmitScalar,
                labScalar,
                subject.id,
                exp.experimentTitle + " - " + biome
            );

            ModuleScienceContainer container = part.FindModuleImplementing<ModuleScienceContainer>();
            if (container != null && container.AddData(data))
            {
                ScreenMessages.PostScreenMessage("[Impact] Recorded " + exp.experimentTitle + " (" + biome + ")", 5f, ScreenMessageStyle.UPPER_LEFT);
                Log("[Impact] Recorded " + exp.experimentTitle + " (" + biome + ")");
                // Optional: pop the review window automatically
                // container.ReviewData();
            }
            else
            {
                // Keep it simple: require a container on the part (recommended for penetrators)
                ScreenMessages.PostScreenMessage("[Impact] No ScienceContainer on part to store data!", 5f, ScreenMessageStyle.UPPER_CENTER);
                Log("[Impact] No ScienceContainer on part to store data!");
            }
        }
        #endregion



        #region UnloadedObserver
        private void RunObserverExperiments(Part observerPart, CelestialBody crashBody, Vessel asteroid, Vessel crashVessel, uint flightID, ConfigNode moduleScienceContainer, ConfigNode scienceNode)
        {
            Log("RunObserverExperiments");
            if (observerPart == null)
                Log("observerPart is null");

            if (!HighLogic.LoadedSceneIsFlight)
            {
                Log("Not in flight");
                return;
            }
            if (firedThisFrame)
            {
                Log("firedThisFrame");
                return;
            }

            Part otherPart = asteroid.Parts[0];
            Part part = crashVessel.Parts[0];
            Log("asteroid: " + asteroid.vesselName + ", crashVessel: " + crashVessel.vesselName);

            if (otherPart == null)
            {
                Log("otherPart is null");
                return;
            }

            bool hitAsteroid = otherPart.vessel != null &&
                               otherPart.vessel.parts.Any(p => p.FindModuleImplementing<ModuleAsteroid>() != null);
            if (!hitAsteroid)
            {
                Log("Did not hit asteroid");
                return;
            }
            double crashVelocity = Math.Abs(crashVessel.srf_velocity.magnitude - asteroid.srf_velocity.magnitude);

            double impactSpeed = crashVelocity;
            if (impactSpeed < minImpactSpeed)
            {
                Log("impactSpeed: " + impactSpeed + ", minImpactSpeed: " + minImpactSpeed);
                return;
            }

            Part asteroidPart = otherPart.vessel.parts.FirstOrDefault(p => p.FindModuleImplementing<ModuleAsteroid>() != null);
            double asteroidMass = asteroidPart != null ? asteroidPart.mass : 0;
            string asteroidClass = GuessAsteroidClassFromMass(asteroidMass);

            FireExperiments(observerPart, impactSpeed, asteroidMass, asteroidClass, moduleScienceContainer, scienceNode);

            firedThisFrame = true;
            part.StartCoroutine(ResetFlagNextFrame());
        }

        //private System.Collections.IEnumerator ResetFlagNextFrame()
        //{
        //    yield return new WaitForFixedUpdate();
        //    firedThisFrame = false;
        //}

        //private string GuessAsteroidClassFromMass(double mass)
        //{
        //    // Simple heuristic; feel free to swap for your pack’s actual mapping
        //    if (mass < 15) return "A";
        //    if (mass < 75) return "B";
        //    if (mass < 300) return "C";
        //    if (mass < 1000) return "D";
        //    return "E";
        //}

        //internal static bool IsSun(CelestialBody body)
        //{
        //    return body.referenceBody == body;
        //}
        //AsteroidImpactSensor ais = null;

        private void FireExperiments(Part part, double vImpact, double mAst, string aClass, ConfigNode moduleScienceContainer, ConfigNode scienceNode)
        {
            Log("FireExperiments (unloaded)");
            ais = part.FindModuleImplementing<AsteroidImpactSensor>();
            if (ais == null)
            {
                Log("No AsteroidImpactSensor found on part: " + part.name);
                return;
            }
            Log("FireExperiments 2");
            // keep your sweet-spot curves, then apply cfg multipliers inside TryRun()
            //TryRun(part, expSeis, vImpact, aClass, ScoreBell(vImpact, 25, 15) + ScoreBell(vImpact, 100, 30), multSeis, impactSensor);
            //TryRun(part, expVolatiles, vImpact, aClass, ScoreBell(vImpact, 80, 25) * (aClass == "C" ? 1.3f : 1.0f), multVolatiles, impactSensor);

            TryRun(part, AsteroidImpactSensor.expEjecta, vImpact, aClass, Plateau(vImpact, 25, 200) * (1.0f + Mathf.Clamp01((float)(mAst / 300.0))), ais.multEjecta, moduleScienceContainer, scienceNode);
            TryRun(part, AsteroidImpactSensor.expMagdust, vImpact, aClass, ScoreBell(vImpact, 60, 22) * (aClass == "E" ? 1.25f : 1.0f), ais.multMagdust, moduleScienceContainer, scienceNode);
            TryRun(part, AsteroidImpactSensor.expAlbedo, vImpact, aClass, Mathf.Clamp01((float)((vImpact - 10.0) / 60.0)), ais.multAlbedo, moduleScienceContainer, scienceNode);

            // Special exception, this must have a seismometer on the surface
            TryRun(part, AsteroidImpactSensor.expRingdown, vImpact, aClass, ScoreBell(vImpact, 40, 18), ais.multRingdown, moduleScienceContainer, scienceNode);
        }

        //[KSPField] public float xmitScalar = 0.6f; // how efficient transmission is
        //[KSPField] public float labScalar = 0.9f; // Mobile Lab processing boost

        private void TryRun(Part part, string expID, double vImpact, string aClass, float quality, float expScalar, ConfigNode moduleScienceContainer, ConfigNode scienceNode)
        {
            Log("TryRun, expID: " + expID + ", vImpact: " + vImpact + ", aClass: " + aClass + ", quality: " + quality + ", expScaler: " + expScalar);

            string experiments = null;

            //Log($"TryRun, part: {part.name}");

            foreach (var p1 in part.partInfo.partConfig.GetNodes("MODULE"))
            {
                DumpNode(p1);

                var name = p1.GetValue("name");
                if (name == "AsteroidImpactSensor")
                {
                    experiments = p1.GetValue("experiments");
                    break;
                }
            }
#if false
            if (experiments == null)
            {
                Log("experiments is null");
            }
#endif
            List<string> validExperiments = experiments.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();

            if (!validExperiments.Contains(expID))
            {
                Log($"TryRun, can't find expID: {expID}");
                return;
            }
            Log($"TryRun, unloaded, found experiment: {expID}");


            if (quality <= 0.05f)
            {
                Log("TryRun, Quality < 0.05f");
                return;
            }

            ScienceExperiment exp = ResearchAndDevelopment.GetExperiment(expID);
            if (exp == null)
            {
                ScreenMessages.PostScreenMessage("[Impact] Experiment " + expID + " not found", 4f, ScreenMessageStyle.UPPER_CENTER);
                Log("Experiment " + expID + " not found");
                return;
            }

            // Stock asteroid subjects are archived under the Sun
            CelestialBody sun = null;

            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                if (IsSun(FlightGlobals.Bodies[i]))
                {
                    sun = FlightGlobals.Bodies[i];
                    break;
                }
            }
            if (sun == null && Planetarium.fetch != null) sun = Planetarium.fetch.Sun;

            string biome = "Asteroid " + aClass;
            ExperimentSituations situation = ExperimentSituations.InSpaceLow;

            ScienceSubject subject = ResearchAndDevelopment.GetExperimentSubject(exp, situation, sun, biome, string.Empty);
            if (subject == null)
            {
                ScreenMessages.PostScreenMessage("[Impact] Could not create subject for " + expID, 4f, ScreenMessageStyle.UPPER_CENTER);
                Log("[Impact] Could not create subject for " + expID);
                return;
            }

            // Apply: curve -> per-experiment -> class
            float classScalar = GetClassScalar(aClass);
            float scaledQuality = Mathf.Max(0.01f, quality) * Mathf.Max(0.01f, expScalar) * Mathf.Max(0.01f, classScalar);

            // KSP1: dataAmount is first arg; cap with experiment limits, then scale by dataScale
            float dataAmount = Mathf.Clamp(scaledQuality * exp.baseValue, 0.1f, exp.scienceCap) * exp.dataScale;

            // KSP1 ctor: ScienceData(float dataAmount, float xmitScalar, float labScalar, string subjectID, string title)
            //ScienceData data = new ScienceData(
            //    dataAmount,
            //    xmitScalar,
            //    labScalar,
            //    subject.id,
            //    exp.experimentTitle + " - " + biome
            //);

            scienceNode.AddValue("data", dataAmount);
            scienceNode.AddValue("scienceValueRatio", xmitScalar);
            scienceNode.AddValue("xmitBonus", labScalar);
            scienceNode.AddValue("subjectID", subject.id);
            scienceNode.AddValue("title", exp.experimentTitle + " - " + biome);
            moduleScienceContainer.AddNode(scienceNode);
            {
                ScreenMessages.PostScreenMessage("[Impact] Recorded " + exp.experimentTitle + " (" + biome + ")", 5f, ScreenMessageStyle.UPPER_LEFT);
                Log("[Impact] Recorded " + exp.experimentTitle + " (" + biome + ")");
                // Optional: pop the review window automatically
                // container.ReviewData();
            }
        }
        #endregion

        private float GetClassScalar(string aClass)
        {
            if (string.IsNullOrEmpty(aClass)) return 1f;
            switch (aClass.Trim().ToUpperInvariant())
            {
                case "A": return Mathf.Max(0f, ais.classMultA);
                case "B": return Mathf.Max(0f, ais.classMultB);
                case "C": return Mathf.Max(0f, ais.classMultC);
                case "D": return Mathf.Max(0f, ais.classMultD);
                case "E": return Mathf.Max(0f, ais.classMultE);
                default: return 1f;
            }
        }

        // curve helpers
        private float ScoreBell(double x, double center, double width)
        {
            double z = (x - center) / width;
            return (float)Math.Exp(-(z * z));
        }
        private float Plateau(double x, double lo, double hi)
        {
            if (x <= lo) return 0f;
            if (x >= hi) return 1f;
            return (float)((x - lo) / (hi - lo));
        }




    }
}