using KSP.UI.Screens;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if false
namespace kerbal_impact
{
    class ImpactCannon : PartModule
    {
        [KSPField]
        public string shellName = "FireworkShell"; // Predefined by the Fireworks module

        [KSPField]
        public float shellDrag = 0.001f;

        [KSPField]
        public float shellMass = 0.1f;

        [KSPField]
        public float maxShots = 10f;

        [KSPField]
        public float shellRBMassScaleValue = 0.05f;

        public GameObject shellPrefab;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#autoLOC_6005104")]
        [UI_FloatRange(stepIncrement = 1f, maxValue = 32f, minValue = 1f)]
        public float impactorsAvailable = 32f;


        [KSPAxisField(minValue = 10f, guiFormat = "F1", isPersistant = true, maxValue = 100f, guiActive = true, guiName = "#autoLOC_6005099")]
        [UI_FloatRange(maxValue = 100f, minValue = 10f)]
        public float shellVelocity = 50f;


        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "#autoLOC_6005104")]
        [UI_Label]
        public string impactorsShotsDisplay = "";

        [KSPField]
        public string cannonName = "COL2";

        private Vector3 shellForceDir = Vector3.forward;

        private WaitForSeconds wfs;

        protected void updateFireworkShotLabel(object field)
        {
            impactorsShotsDisplay = impactorsAvailable.ToString("F0");
        }


        public override void OnAwake()
        {
            ImpactMonitor.Log("Cannon.OnAwake");
            shellPrefab = AssetBase.GetPrefab(shellName);
            wfs = new WaitForSeconds(1f);
            base.Fields["impactorsAvailable"].OnValueModified += updateFireworkShotLabel;

            if (shellPrefab == null)
            {
                base.Events["LaunchShell"].guiActive = false;
                return;
            }

        }

        [KSPEvent(guiName = "Fire", guiActive = true, guiActiveEditor = false, active = true)]
        public void LaunchShell()
        {
            if (shellPrefab == null)
            {
                return;
            }
            ImpactMonitor.Log("Cannon.Fire");

            if (!CheatOptions.InfinitePropellant)
            {
                if (!(impactorsAvailable > 0f))
                {
                    return;
                }
                impactorsAvailable -= 1f;
                updateFireworkShotLabel(null);
            }
            Transform transform = base.gameObject.GetChild(cannonName).transform;
            Vector3 vector;
            if (!(transform != null))
            {
                vector = Vector3.zero;
            }
            else
            {
                vector = transform.position;
            }
            Vector3 position = vector;
            GameObject gameObject = UnityEngine.Object.Instantiate(shellPrefab, position, Quaternion.identity);
            Renderer componentInChildren = gameObject.GetComponentInChildren<Renderer>();
            if (componentInChildren != null)
            {
                if (componentInChildren.material.mainTexture == null)
                {
                    componentInChildren.material.SetTexture("_MainTex", base.part.GetPartRenderers()[0].material.mainTexture);
                }
            }
#if false
            fxController = gameObject.GetComponent<FireworkFX>();
            if (FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphere)
            {
                if (FlightGlobals.ActiveVessel.altitude < FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth)
                {
                    configureSoundFX();
                }
            }
#endif

            physicalObject obj = physicalObject.ConvertToPhysicalObject(base.part, gameObject);
            Rigidbody rb = obj.rb;
            obj.maxDistance = 10000f;
            obj.origDrag = shellDrag;
            rb.mass = shellMass * shellRBMassScaleValue;
            rb.maxAngularVelocity = PhysicsGlobals.MaxAngularVelocity;
            rb.angularVelocity = base.part.Rigidbody.angularVelocity;
            gameObject.transform.rotation = base.transform.rotation;
            shellForceDir = transform.up;
#if false
            if (variationOnShellDirection)
            {
                shellForceDir = (shellForceDir + UnityEngine.Random.onUnitSphere * variationOnShellDirMultiplier).normalized;
            }
#endif
            rb.drag = shellDrag;
            rb.useGravity = false;
            Vector3 force = shellForceDir * shellMass * (shellVelocity / Time.fixedDeltaTime) * shellRBMassScaleValue;
            rb.AddForce(force, ForceMode.Force);
            if (Krakensbane.GetFrameVelocity().magnitude <= 0.0)
            {
                rb.AddForce(base.vessel.rb_velocityD.normalized * shellMass * (base.vessel.rb_velocityD.magnitude / (double)Time.fixedDeltaTime) * shellRBMassScaleValue, ForceMode.Force);
            }
            base.part.AddForce(force.normalized * shellMass * shellVelocity * shellRBMassScaleValue * -1f);
#if false
            matchColorPickers();
            fxController.Setup(shellDuration, getCurrentPSByType(FireworkEffectType.TRAIL), getCurrentPSByType(FireworkEffectType.BURST), fireworkColors[0], fireworkColors[1], fireworkColors[3], fireworkColors[2], fireworkColors[4], shellVelocity, burstSpread, burstDuration, burstFlareSize, getCurrentFXByType(FireworkEffectType.BURST).crackleSFX, getCurrentFXByType(FireworkEffectType.BURST).randomizeBurstOrientation, getCurrentFXByType(FireworkEffectType.TRAIL).minTrailLifetime, getCurrentFXByType(FireworkEffectType.TRAIL).maxTrailLifetime);
#endif
            ImpactMonitor.Log("Cannon.LaunchShell, obj created");

            Vessel v = PhysicalObjectToVessel_ConvertAndSpawn(obj, "impactProjectile", "Impactor", vessel);
        }


        public Vessel PhysicalObjectToVessel_ConvertAndSpawn(physicalObject physObj, string basePartName, string vesselName, Vessel parentVessel)
        {
            ImpactMonitor.Log("Cannon.LaunchShell, ConvertAndSpawn");

            if (physObj == null)
            {
                ImpactMonitor.LogError("[PO->Vessel] PhysicalObject is null!");
                return null;
            }

            // STEP 1: Get base part prefab
            AvailablePart basePrefabInfo = PartLoader.getPartInfoByName(basePartName);
            if (basePrefabInfo == null)
            {
                ImpactMonitor.LogError($"[PO->Vessel] Could not find base part '{basePartName}'.");
                return null;
            }

            ImpactMonitor.Log("Cannon.LaunchShell, ConvertAndSpawn, instantiating part");

            // STEP 2: Instantiate Part
            Part newPart = UnityEngine.Object.Instantiate(basePrefabInfo.partPrefab);
            newPart.gameObject.name = physObj.name + "_Part";
            newPart.transform.position = physObj.transform.position;
            newPart.transform.rotation = physObj.transform.rotation;
            newPart.transform.localScale = physObj.transform.localScale;

            ImpactMonitor.Log("Cannon.LaunchShell, ConvertAndSpawn, partName: " + newPart.gameObject.name);

            // STEP 3: Replace model
            Transform oldModel = newPart.transform.Find("model");
            if (oldModel != null)
            {
                ImpactMonitor.Log("Cannon.LaunchShell, ConvertAndSpawn, old model found, destroying it");
                UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
            }
            foreach (Transform child in physObj.transform)
                child.SetParent(newPart.transform, true);

            // STEP 4: Remove PhysicalObject
            UnityEngine.Object.DestroyImmediate(physObj);

            ImpactMonitor.Log("Cannon.LaunchShell, Step 5");
            // STEP 5: Ensure Rigidbody
            if (!newPart.TryGetComponent<Rigidbody>(out _))
                newPart.gameObject.AddComponent<Rigidbody>().mass = 1f;

#if false
                ImpactMonitor.Log("Cannon.LaunchShell, ConvertAndSpawn, checking for ModuleCommand");

                // STEP 6: Add ModuleCommand so it’s controllable
                //if (newPart.Modules == null || newPart.Modules.Count == 0 || newPart.Modules.Contains("ModuleCommand"))
                {
                    ImpactMonitor.Log("Cannon.LaunchShell, ConvertAndSpawn, adding ModuleCommand");
                    ModuleCommand cmd = newPart.AddModule("ModuleCommand") as ModuleCommand;
                    if (cmd == null)
                        ImpactMonitor.Log("Cannon.LaunchShell, unable to add ModuleCommand");

                    cmd.minimumCrew = 0;
                }
#endif

#if false
                // STEP 7: Add ElectricCharge as a Part resource (KSP1 method)
                if (newPart.Resources.Get("ElectricCharge") == null)
                {
                    ConfigNode ecNode = new ConfigNode("RESOURCE");
                    ecNode.AddValue("name", "ElectricCharge");
                    ecNode.AddValue("amount", 50.0);
                    ecNode.AddValue("maxAmount", 50.0);
                    newPart.AddResource(ecNode);
                }
#endif

            ImpactMonitor.Log("Cannon.LaunchShell, Step 8");
            // STEP 8: Initialize modules
            //newPart.gameObject.SetActive(true);
            //newPart.InitializeModules();

            // following code taken from KSPGeoCaching
            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.1");

            ConfigNode empty = new ConfigNode();
            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.1.1");
            ProtoVessel dummyProto = new ProtoVessel(empty, null);

            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.1.2");
            Vessel dummyVessel = new Vessel();
            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.1.3");
            dummyVessel.parts.Add(newPart);
            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.1.4");
            dummyProto.vesselRef = dummyVessel;
            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.2");
            // Create the ProtoPartSnapshot objects and then initialize them
            foreach (Part p in dummyVessel.parts)
            {
                dummyProto.protoPartSnapshots.Add(new ProtoPartSnapshot(p, dummyProto));
            }
            foreach (ProtoPartSnapshot p in dummyProto.protoPartSnapshots)
            {
                p.storePartRefs();
            }
            // Create the ship's parts
            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.3");

            ConfigNode[] partNodes;
            List<ConfigNode> partNodesL = new List<ConfigNode>();
            foreach (ProtoPartSnapshot snapShot in dummyProto.protoPartSnapshots)
            {
                ConfigNode node = new ConfigNode("PART");
                snapShot.Save(node);
                partNodesL.Add(node);
            }
            partNodes = partNodesL.ToArray();
            // Create additional nodes
            ConfigNode[] additionalNodes = new ConfigNode[0];
            // Create the config node representation of the ProtoVessel
            ImpactMonitor.Log("Cannon.LaunchShell, Step 8.6");
            ConfigNode protoVesselNode = ProtoVessel.CreateVesselNode(vesselName, VesselType.Probe, parentVessel.orbit, 0, partNodes, additionalNodes);


            ProtoVessel protoVessel = HighLogic.CurrentGame.AddVessel(protoVesselNode);

            ImpactMonitor.Log("Cannon.LaunchShell, " + " Before Coroutine");

            StartCoroutine(PlaceSpawnedVessel(protoVessel.vesselRef, false));

#if false
            // STEP 9: Build Vessel ConfigNode
            ConfigNode vesselNode = new ConfigNode("VESSEL");
            vesselNode.AddValue("pid", Guid.NewGuid().ToString());
            vesselNode.AddValue("name", vesselName);
            vesselNode.AddValue("type", VesselType.Ship);
            vesselNode.AddValue("sit", Vessel.Situations.ORBITING);
            vesselNode.AddValue("landed", false);
            vesselNode.AddValue("lat", 0);
            vesselNode.AddValue("lon", 0);
            vesselNode.AddValue("alt", 1000);
            vesselNode.AddValue("hgt", 1);
            vesselNode.AddValue("nrm", "0,1,0");
            vesselNode.AddValue("rot", KSPUtil.WriteQuaternion(newPart.transform.rotation));
            vesselNode.AddValue("CoM", KSPUtil.WriteVector(newPart.transform.position));
            vesselNode.AddValue("stg", 0);
            vesselNode.AddValue("prst", false);

            // STEP 10: Save the Part into the vessel node
            ProtoPartSnapshot pps = new ProtoPartSnapshot(newPart, null);
            pps.Save(vesselNode.AddNode("PART"));

            ImpactMonitor.Log("Cannon.LaunchShell, STEP 11");

            // STEP 11: Create ProtoVessel and load it into current FlightState
            ProtoVessel protoVessel = new ProtoVessel(vesselNode, HighLogic.CurrentGame);
            protoVessel.Load(HighLogic.CurrentGame.flightState);
#endif
            // STEP 12: Find the spawned vessel and set it active
            Vessel vessel = FlightGlobals.Vessels.Find(v => v.protoVessel == protoVessel);
            if (vessel != null)
            {
                FlightGlobals.SetActiveVessel(vessel);
                ImpactMonitor.Log($"[PO->Vessel] Converted {physObj.name} into controllable vessel '{vesselName}'.");
            }
            else
            {
                ImpactMonitor.LogError("[PO->Vessel] Vessel spawn failed!");
            }

            return vessel;
        }
        private IEnumerator PlaceSpawnedVessel(Vessel v, bool moveVessel)
        {
            ImpactMonitor.Log("Cannon.LaunchShell, PlaceSpawnedVessel");
            //loadingCraft = true;
            v.isPersistent = true;
            v.Landed = false;
            v.situation = Vessel.Situations.FLYING;
            while (v.packed)
            {
                yield return null;
            }
            v.SetWorldVelocity(Vector3d.zero);

            yield return null;
            //FlightGlobals.ForceSetActiveVessel(v);
            //spawnedVessel = v;
            yield return null;
            v.Landed = true;
            v.situation = Vessel.Situations.PRELAUNCH;
            v.GoOffRails();
            v.IgnoreGForces(240);


            //Staging.beginFlight();
            StageManager.BeginFlight();

        }



    }
}
#endif