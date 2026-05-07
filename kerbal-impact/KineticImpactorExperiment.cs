using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{
    internal class KineticImpactorExperiment : PartModule, IPartMassModifier
    {
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Impactors Available"),
        UI_ProgressBar(affectSymCounterparts = UI_Scene.None, controlEnabled = false, scene = UI_Scene.Flight, maxValue = 4, minValue = 0, requireFullControl = false)]
        public float impactorsLeft = 4;

        [KSPField]
        public string availableImpactorNames = null;

        [KSPField(guiName = "Projectile Type", isPersistant = true, guiActive = false, guiActiveEditor = true), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string selectedImpactor = null;

        [KSPField]
        public float reloadTime = 5f;

        [KSPField]
        public string launchTransformName = "";

        [KSPField]
        public Vector3 launchTransformForward = Vector3.forward;

        [KSPField]
        public float refireDelay = 4;

        [KSPField]
        public float offset = 0; //if the spawn point needs to be adjusted

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Impactor Launch Velocity")]
        [UI_FloatRange(stepIncrement = 1f, maxValue = 1000f, minValue = 1f)]
        public float impactorVelocity = 750; //how fast the impactor will be fired at the target

        [KSPField]
        public float defaultImpactorVelocity = 750;

        [KSPField]
        public string vesselNameTemplate = "Impact Probe";

        [Persistent]
        int spawnedCnt = 0;


        public float timeFired; // Note: this is technically off by Time.fixedDeltaTime (since it's meant to be within the range [Time.time <—> Time.time + Time.fixedDeltaTime]), but so is Time.time in timeSinceFired, so we can skip adding the constant.
        public float timeSinceFired => Time.time - timeFired;

        AvailablePart ImpactorPart;

        [KSPField]
        public Vector3 impactorTransformForward = Vector3.forward;

        public Part SpawnedImpactor;

        // Launcher vars
        Transform launchTransform;

        // Impactor variables
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => impactorsLeft * ImpactorMass; //*

        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED; //*

        private float ImpactorMass = 0.01f; //*

        private static string[] impactorList = null;
        private static string[] impactorDescrList = null;
        private static AvailablePart[] impactorPartList = null;

        static string newVesselName;

        private void getImpactorList()
        {
            LogWarning("getImpactorList");
            if (impactorList == null)
            {
                LogWarning("getImpactorList 2");
                List<string> options = new List<string>();

                List<string> items = availableImpactorNames
                    .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()) // optional: removes extra spaces
                    .ToList();

                impactorList = items.ToArray();
                impactorDescrList = new string[impactorList.Length];
                impactorPartList = new AvailablePart[impactorList.Length];
            }
        }

        private void initializeImpactors()
        {
            LogWarning("initializeImpactors");
            if (availableImpactorNames != null)
            {
                LogWarning("initializeImpactors, availableImpactorNames: " + availableImpactorNames);
                if (impactorList == null)
                    getImpactorList();

                BaseField field = Fields["selectedImpactor"];
                UI_ChooseOption range = (UI_ChooseOption)field.uiControlEditor;
                if (range != null)
                {
                    range.onFieldChanged = UpdateImpactorAfterSelection; //
                    range.options = impactorDescrList;
                }
            }
        }

        public override void OnStart(PartModule.StartState state)
        {
            initializeImpactors();
            if (selectedImpactor == null || selectedImpactor == "")
            {
                selectedImpactor = impactorDescrList[0];
                LogWarning("OnStart, selectedImpactor: " + selectedImpactor);
            }

            this.enabled = true;
            this.part.force_activate();
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                StartCoroutine(GetAllImpactors());

                launchTransform = part.FindModelTransform(launchTransformName); //this is operating on the assumption that transform.forward is pointing in the direction you want it to launch
                if (launchTransform == null) launchTransform = part.transform; //which means defaulting to the root transform might not shoot in the right direction
                if (HighLogic.LoadedSceneIsEditor)
                    impactorVelocity = defaultImpactorVelocity;
            }
        }

        #region part spawning
        public bool SpawnImpactor(Transform Transform, float offset = 0)
        {
            LogWarning("SpawnImpactor, impactorsLeft: " + impactorsLeft);
            if (impactorsLeft >= 1)
            {
                if (ImpactorPart != null)
                {
                    if (Transform == null)
                        Transform = part.partTransform;

                    foreach (PartModule m in ImpactorPart.partPrefab.Modules)
                    {
                        if (m.moduleName == "KineticImpactor") 
                        {
                            var partNode = new ConfigNode();
                            PartSnapshot(ImpactorPart.partPrefab).CopyTo(partNode);
                            //SpawnedImpactor = CreatePart(partNode, offset > 0 ? (Transform.position + Transform.forward * offset) : Transform.transform.position, Transform.rotation, this.part);
                            SpawnedImpactor = CreatePart(partNode, offset > 0 ? (Transform.position + Transform.rotation * launchTransformForward * offset) : Transform.transform.position, Transform.rotation, this.part);

                            impactorsLeft--;
                            if (impactorsLeft == 0)
                            {
                                Events["GUILaunchImpactor"].guiActive = false;

                            }

                            SpawnedImpactor.vessel.vesselType = VesselType.Probe;
                            SpawnedImpactor.vessel.vesselName = newVesselName; // "Impactor Probe";

                            Transform partTransform = SpawnedImpactor.transform;
                            Vector3 vesselForward = launchTransform.rotation * impactorTransformForward;

                            Quaternion targetRot = Quaternion.LookRotation(vesselForward, part.transform.up);
                            SpawnedImpactor.transform.rotation = targetRot;

                            return true;
                        }
                    }
                    LogWarning("Module KineticImpactor not found in part");
                }
                else
                    LogWarning("ImpactPart is null");
            }
            return false;
        }

        IEnumerator GetAllImpactors()
        {
            yield return new WaitForFixedUpdate();
            int i = 0;
            foreach (var s in impactorList)
            {
                using (var parts = PartLoader.LoadedPartsList.GetEnumerator())
                {
                    while (parts.MoveNext())
                    {
                        if (parts.Current.partConfig == null || parts.Current.partPrefab == null)
                            continue;
                        if (parts.Current.partPrefab.partInfo.name != s)
                            continue;


                        impactorDescrList[i] = parts.Current.title;
                        impactorPartList[i] = parts.Current;
                        //LogWarning("GetAllImpactors, impactor: " + s + ", title: " + parts.Current.title + ", " + parts.Current.title);
                        i++;
                        break;
                    }
                }
            }
            GetCurrentImpactor();
        }

        void GetCurrentImpactor()
        {
            //yield return new WaitForFixedUpdate();
            using (var parts = PartLoader.LoadedPartsList.GetEnumerator())
                while (parts.MoveNext())
                {
                    if (parts.Current.partConfig == null || parts.Current.partPrefab == null)
                        continue;
                    if (parts.Current.partPrefab.partInfo.title != selectedImpactor)
                        continue;


                    ImpactorPart = parts.Current;
                    //LogWarning($"GetCurrentImpactor, impactor found: {selectedImpactor}!");

                    break;
                }
            if (ImpactorPart == null)
            {
                LogWarning($"GetCurrentImpactor, Failed to find impactor: {selectedImpactor}!");
                return;
                //yield break;
            }
            ImpactorMass = ImpactorPart.partPrefab.mass;
        }

        void UpdateImpactorAfterSelection(BaseField field, object o)
        {
            //LogWarning("UpdateImpactor, o: " + (string)o);

            for (int i = 0; i < impactorDescrList.Length; i++)
            {
                //LogWarning("UpdateImpactor, descr: " + impactorDescrList[i]);
                if (impactorDescrList[i] == (string)o)
                {
                    ImpactorPart = impactorPartList[i];
                    ImpactorMass = ImpactorPart.partPrefab.mass;
                    //LogWarning("SelectedPart: " + ImpactorPart.name);
                    return;
                }
            }
        }


        static IEnumerator FinalizeImpactor(Part impactor, Part launcher)
        {
            string originatingVesselName = impactor.vessel.vesselName;
            impactor.physicalSignificance = Part.PhysicalSignificance.NONE;
            impactor.PromoteToPhysicalPart();
            //depending on the geometry of the launcher and colliders it has, this might be unnecessary; but will ensure it spawns cleanly

            var childColliders = impactor.GetComponentsInChildren<Collider>(includeInactive: false);
            CollisionManager.IgnoreCollidersOnVessel(launcher.vessel, childColliders);
            foreach (var col in childColliders)
                col.enabled = false;

            impactor.Unpack();
            impactor.InitializeModules();
            Vessel newVessel = impactor.gameObject.AddComponent<Vessel>();
            newVessel.id = Guid.NewGuid();
            if (newVessel.Initialize(false))
            {
                newVessel.vesselType = VesselType.Probe;

                newVessel.vesselName = Vessel.AutoRename(newVessel, newVesselName); // "Impactor Probe"); 
                newVessel.IgnoreGForces(10);
                newVessel.currentStage = KSP.UI.Screens.StageManager.RecalculateVesselStaging(newVessel); //shouldn't have any, but just in case
                impactor.setParent(null);
            }

            yield return new WaitWhile(() => !impactor.started && impactor.State != PartStates.DEAD);
            newVessel.vesselType = VesselType.Probe;

            if (impactor.State == PartStates.DEAD)
            {
                Log($"Error: {impactor} died before being fully initialized");
                yield break;
            }
        }

        public static ConfigNode PartSnapshot(Part part)
        {
            var node = new ConfigNode("PART");
            var snapshot = new ProtoPartSnapshot(part, null);

            snapshot.attachNodes = new List<AttachNodeSnapshot>();
            snapshot.srfAttachNode = new AttachNodeSnapshot("attach,-1");
            snapshot.symLinks = new List<ProtoPartSnapshot>();
            snapshot.symLinkIdxs = new List<int>();
            snapshot.Save(node);

            // Prune unimportant data
            node.RemoveValues("parent");
            node.RemoveValues("position");
            node.RemoveValues("rotation");
            node.RemoveValues("istg");
            node.RemoveValues("dstg");
            node.RemoveValues("sqor");
            node.RemoveValues("sidx");
            node.RemoveValues("attm");
            node.RemoveValues("srfN");
            node.RemoveValues("attN");
            node.RemoveValues("connected");
            node.RemoveValues("attached");
            node.RemoveValues("flag");
            node.RemoveNodes("ACTIONS");

            var module_nodes = node.GetNodes("MODULE");
            var prefab_modules = part.partInfo.partPrefab.GetComponents<PartModule>();
            node.RemoveNodes("MODULE");

            for (int i = 0; i < prefab_modules.Length && i < module_nodes.Length; i++)
            {
                var module = module_nodes[i];
                var name = module.GetValue("name") ?? "";

                node.AddNode(module);
                module.RemoveNodes("ACTIONS");
            }
            return node;
        }

        public delegate void OnPartReady(Part affectedPart);

        /// <summary>Creates a new part from the config.</summary>
        /// <param name="partConfig">Config to read part from.</param>
        /// <param name="position">Initial position of the new part.</param>
        /// <param name="rotation">Initial rotation of the new part.</param>
        /// <param name="fromPart"></param>

        public static Part CreatePart(
            ConfigNode partConfig,
            Vector3 position,
            Quaternion rotation,
            Part launcherPart)
        {
            var refVessel = launcherPart.vessel;
            var partNodeCopy = new ConfigNode();
            partConfig.CopyTo(partNodeCopy);
            var snapshot =
                new ProtoPartSnapshot(partNodeCopy, refVessel.protoVessel, HighLogic.CurrentGame);
            if (HighLogic.CurrentGame.flightState.ContainsFlightID(snapshot.flightID)
                || snapshot.flightID == 0)
            {
                snapshot.flightID = ShipConstruction.GetUniqueFlightID(HighLogic.CurrentGame.flightState);
            }
            snapshot.parentIdx = 0;
            snapshot.position = position;
            snapshot.rotation = rotation;
            snapshot.stageIndex = 0;
            snapshot.defaultInverseStage = 0;
            snapshot.seqOverride = -1;
            snapshot.inStageIndex = -1;
            snapshot.attachMode = (int)AttachModes.SRF_ATTACH;
            snapshot.attached = false;

            var newPart = snapshot.Load(refVessel, false);
            newPart.transform.position = position;
            newPart.transform.rotation = rotation;
            if (newPart.rb != null)
            {
                newPart.rb.velocity = launcherPart.Rigidbody.velocity;
                newPart.rb.angularVelocity = launcherPart.Rigidbody.angularVelocity;
            }
            newPart.missionID = launcherPart.missionID;
            newPart.UpdateOrgPosAndRot(newPart.vessel.rootPart);

            newPart.StartCoroutine(FinalizeImpactor(newPart, launcherPart));
            return newPart;
        }
        #endregion

        #region ImpactorGun
        [KSPAction("Launch Impactor")]
        public void AGLaunchImpactor(KSPActionParam param)
        {
            LaunchImpactor();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Launch Impactor", active = true)]
        public void GUILaunchImpactor()
        {
            LaunchImpactor();
        }

        void LaunchImpactor()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (vessel.situation <= Vessel.Situations.PRELAUNCH)
            {
                ScreenMessages.PostScreenMessage("Cannot launch impactor on the launchpad, landed or splashed down");
                return; 
            }
            if (impactorsLeft < 1)
            {
                return; //out of impactors
            }
            if (timeSinceFired < refireDelay) return; //still in cooldown

            timeFired = Time.time;

            spawnedCnt++;
            newVesselName = vesselNameTemplate + " #" + spawnedCnt.ToString();
            StartCoroutine(SpawnImpactor());
        }

        IEnumerator SpawnImpactor()
        {
            if (!SpawnImpactor(launchTransform, offset))
            {
                LogWarning($"Failed to spawn impactor from {this.part.partInfo.title} on {this.part.vessel.GetName()}");
                yield break;
            }
            KineticImpactor ml = SpawnedImpactor.FindModuleImplementing<KineticImpactor>(); //module on the impactor, placeholder name for now 
            yield return new WaitUntilFixed(() => ml == null || ml.SetupComplete); // Wait until impactor fully initialized.

            if (ml == null || ml.gameObject == null || !ml.gameObject.activeInHierarchy)
            {
                if (ml != null) Destroy(ml); // The gameObject is gone, make sure the module goes too.
                yield break; // The impactor died for some reason, abort
            }
            //SpawnedImpactor.rb.velocity += impactorVelocity * launchTransform.forward; //launch it forward
            //part.rb.AddForceAtPosition(-launchTransform.forward * (impactorVelocity * ImpactorMass), launchTransform.position, ForceMode.Impulse); //for every action, an opposite and equal reaction... optional

            Log("impactorVelocity: " + impactorVelocity.ToString("F0"));
            SpawnedImpactor.rb.velocity += impactorVelocity * (launchTransform.rotation * launchTransformForward); //launch it forward

            if (part.rb != null)
            {
                part.rb.AddForceAtPosition(-(launchTransform.rotation * launchTransformForward) * (impactorVelocity * ImpactorMass), launchTransform.position, ForceMode.Impulse); //for every action, an opposite and equal reaction... optional

                Log("SpawnImpactor, part: " + part.partInfo.title +
                    ", impactorVelocity * ImpactorMass: " + impactorVelocity * ImpactorMass +
                    ", AddForceAtPosition: " + -(launchTransform.rotation * launchTransformForward) * (impactorVelocity * ImpactorMass));
            }
            else
                LogWarning("No rigidbody found for part: " + part.partInfo.title);
            StartCoroutine(SwitchToVesselWhenPossible(SpawnedImpactor.vessel));// and switch to it
        }

        public IEnumerator SwitchToVesselWhenPossible(Vessel vessel, float distance = 0)
        {
            ImpactMonitor.instance.LastActiveLauncher = this.vessel;
            var wait = new WaitForFixedUpdate();
            while (vessel != null && (!vessel.loaded || vessel.packed)) yield return wait;
            while (vessel != null && vessel.loaded && vessel != FlightGlobals.ActiveVessel)
            {
                ForceSwitchVessel(vessel);
                yield return wait;
            }
            if (vessel != null && vessel.loaded && !vessel.packed)
            {
                var flightCam = FlightCamera.fetch;
                if (flightCam != null && distance > 0) flightCam.SetDistance(distance);
            }
        }

        public void ForceSwitchVessel(Vessel v)
        {

            if (v == null || !v.loaded)
                return;
            var camHeading = FlightCamera.CamHdg;
            var camPitch = FlightCamera.CamPitch;
            FlightGlobals.ForceSetActiveVessel(v);
            FlightInputHandler.ResumeVesselCtrlState(v);
            FlightCamera.CamHdg = camHeading;
            FlightCamera.CamPitch = camPitch;
        }
        #endregion
    }

    public class WaitUntilFixed : IEnumerator
    {
        private WaitForFixedUpdate wait = new WaitForFixedUpdate();
        public virtual object Current => wait;
        Func<bool> predicate;

        public WaitUntilFixed(Func<bool> predicate)
        {
            this.predicate = predicate;
        }

        public bool MoveNext() => !predicate();
        public virtual void Reset() { }
    }
}