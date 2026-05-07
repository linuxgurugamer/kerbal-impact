using KSP.UI.Screens;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static kerbal_impact.ImpactMonitor;

namespace kerbal_impact
{
    public class ImpactNodeController : PartModule
    {
        //[KSPField(guiActive = true, guiActiveEditor = true, isPersistant = true, guiName = "Node Enabled")]
        //[UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
        public bool nodesEnabled = true;

        List<string> attachNodesList = new List<string>();
        List<string> probeMountingNodesList = new List<string>();

        static float tmpThrust = 0;
        static ITargetable targetVessel = null;

        void LaunchProbe(string nodeName, float thrust)
        {
            var n = probeMountNodeDict[nodeName];
            LaunchProbe(n, thrust);
        }

        void LaunchProbe(nodeWrapper n, float thrust)
        {
            targetVessel = null;

            if (n.AttachNode == null)
            {
                Log("n.AttachNode is null");
                return;
            }
            if (n.AttachNode.attachedPart == null)
            {
                Log("n.AttachNode.attachedPart is null");
                return;
            }
            ModuleEnginesFX probeEngine = n.AttachNode.attachedPart.FindModuleImplementing<ModuleEnginesFX>();
            if (probeEngine != null)
            {
                tmpThrust = thrust;
            }
            else
                Log("engine not found in probe");
            if (FlightGlobals.fetch.VesselTarget != null)
            {
                targetVessel = FlightGlobals.fetch.VesselTarget;
            }
            PartResource resource = n.AttachNode.attachedPart.Resources.FirstOrDefault(r => r.resourceName == "ElectricCharge");
            if (resource != null)
            {
                resource.amount = resource.maxAmount; ;
                Debug.Log($"Set EC amount to {resource.maxAmount} units of ElectricCharge to {part.partName}. New amount: {resource.amount}");
            }
            else
            {
                Debug.LogWarning($"No ElectricCharge resource found on {part.partName}.");
            }

            ModuleDecouple md = n.AttachNode.attachedPart.FindModuleImplementing<ModuleDecouple>();

            if (md != null)
            {
                md.Decouple();
            }
            UpdateEventFields();
        }

        #region EVENTS

        #region box01
        [KSPEvent(guiActiveEditor = false, guiActive = false, guiName = "Launch #1", active = true, groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        public void Launch_1()
        {
            nodeWrapper n;
            if (probeMountNodeDict.ContainsKey("top01"))
                n = probeMountNodeDict["top01"];
            else
                n = probeMountNodeDict["top"];
            LaunchProbe(n, probe1Thrust);

        }
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false, guiName = "Thrust for probe in box 1",
            groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        [UI_FloatRange(stepIncrement = .1f, maxValue = 100f, minValue = .01f)]
        public float probe1Thrust = 100f;
        #endregion

        #region box02
        [KSPEvent(guiActiveEditor = false, guiActive = false, guiName = "Launch #2", active = true, groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        public void Launch_2()
        {
            LaunchProbe("top02", probe2Thrust);
        }
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false, guiName = "Thrust for probe in box 2",
            groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        [UI_FloatRange(stepIncrement = .1f, maxValue = 100f, minValue = .01f)]
        public float probe2Thrust = 100f;
        #endregion

        #region box03
        [KSPEvent(guiActiveEditor = false, guiActive = false, guiName = "Launch #3", active = true, groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        public void Launch_3()
        {
            LaunchProbe("top03", probe3Thrust);
        }
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false, guiName = "Thrust for probe in box 3",
            groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        [UI_FloatRange(stepIncrement = .1f, maxValue = 100f, minValue = .01f)]
        public float probe3Thrust = 100f;
        #endregion

        #region box04
        [KSPEvent(guiActiveEditor = false, guiActive = false, guiName = "Launch #4", active = true, groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        public void Launch_4()
        {
            LaunchProbe("top04", probe4Thrust);
        }
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiActiveUnfocused = false, guiName = "Thrust for probe in box 4",
            groupName = "Probes", groupDisplayName = "Probe Launch Menu", groupStartCollapsed = false)]
        [UI_FloatRange(stepIncrement = .1f, maxValue = 100f, minValue = .01f)]
        public float probe4Thrust = 100f;
        #endregion

        #endregion

        internal class nodeWrapper
        {
            internal AttachNode AttachNode { get; set; }
            internal int index;

            internal nodeWrapper(AttachNode node, int i)
            {
                AttachNode = node;
                index = i;
            }
        }

        Dictionary<string, nodeWrapper> nodeDict = new Dictionary<string, nodeWrapper>();
        Dictionary<string, nodeWrapper> probeMountNodeDict = new Dictionary<string, nodeWrapper>();

        void LoadNodeInfo()
        {
            foreach (var p1 in part.partInfo.partConfig.GetNodes("MODULE"))
            {
                var name = p1.GetValue("name");
                if (name == "ImpactNodeController")
                {
                    if (p1.HasNode("ATTACH_NODES"))
                    {
                        ConfigNode configNode = p1.GetNode("ATTACH_NODES");
                        attachNodesList = configNode.GetValuesList("node");
                    }
                    if (p1.HasNode("PROBE_MOUNTING_NODES"))
                    {
                        ConfigNode configNode = p1.GetNode("PROBE_MOUNTING_NODES");
                        probeMountingNodesList = configNode.GetValuesList("node");
                    }
                }
                break;
            }

            nodeDict.Clear();
            foreach (var s in attachNodesList)
            {
                AttachNode n = part.FindAttachNode(s);
                if (n != null)
                    nodeDict.Add(s, new nodeWrapper(n, part.attachNodes.IndexOf(n)));

            }
            probeMountNodeDict.Clear();
            for (int i = 0; i < probeMountingNodesList.Count; i++)
            {
                var s = probeMountingNodesList[i];
                AttachNode n = part.FindAttachNode(s);
                if (n != null)
                {
                    probeMountNodeDict.Add(s, new nodeWrapper(n, part.attachNodes.IndexOf(n)));
                }
            }
            UpdateEventFields();
        }

        void UpdateEventFields()
        {
            for (int i = 0; i < probeMountingNodesList.Count; i++)
            {
                var s = probeMountingNodesList[i];

                AttachNode n = part.FindAttachNode(s);
                if (n != null)
                {
                    //probeMountNodeDict.Add(s, new nodeWrapper(n, part.attachNodes.IndexOf(n)));
                    if (n.attachedPart != null)
                    {
                        if (HighLogic.LoadedSceneIsFlight)
                            Events["Launch_" + (i + 1).ToString()].active = Events["Launch_" + (i + 1).ToString()].guiActive = true;
                        Fields["probe" + (i + 1).ToString() + "Thrust"].guiActive =
                            Fields["probe" + (i + 1).ToString() + "Thrust"].guiActiveEditor = true;
                    }
                    else
                    {
                        if (HighLogic.LoadedSceneIsFlight)
                            Events["Launch_" + (i + 1).ToString()].active = Events["Launch_" + (i + 1).ToString()].guiActive = false;
                        Fields["probe" + (i + 1).ToString() + "Thrust"].guiActive =
                            Fields["probe" + (i + 1).ToString() + "Thrust"].guiActiveEditor = false;
                    }
                }
                else
                    Log($"Part: {part.partName}  probe mounting node NOT found: " + i);
            }
            for (int i = probeMountingNodesList.Count; i < 4; i++)
            {
                if (HighLogic.LoadedSceneIsFlight)
                    Events["Launch_" + (i + 1).ToString()].active = Events["Launch_" + (i + 1).ToString()].guiActive = false;
                Fields["probe" + (i + 1).ToString() + "Thrust"].guiActive =
                    Fields["probe" + (i + 1).ToString() + "Thrust"].guiActiveEditor = false;
            }
        }

        bool Placed()
        {
            if (part.parent != null || EditorLogic.RootPart == this.part)
                return true;
            return false;
        }

        #region StartDestroy
        void Start()
        {
            LoadNodeInfo();

            GameEvents.onEditorPartEvent.Add(onEditorPartEvent);
            GameEvents.onVesselCreate.Add(onVesselCreate);

            if (Placed())
                SetAttachNodes(false);
            else
                SetAttachNodes(true);
        }

        void OnDestroy()
        {
            GameEvents.onEditorPartEvent.Remove(onEditorPartEvent);
            GameEvents.onVesselCreate.Remove(onVesselCreate);
        }
        #endregion

        void onVesselCreate(Vessel v)
        {
            if (v.Parts.Count > 0)
            {
                Log($"onVesselCreate, vessel: {v.vesselName}  rootPart: {v.Parts[0].name}");
                if (v.Parts[0].name == "sci-impact-probeslug" ||
                    v.Parts[0].name == "ImpactProbe" ||
                    v.Parts[0].name == "ImpactProbeII")
                        StartCoroutine(SwitchToVesselWhenPossible(v));
            }
            else
                Log($"onVesselCreate, v.Parts.Count is 0, vessel: {v.vesselName}");
        }

        public IEnumerator SwitchToVesselWhenPossible(Vessel vessel, float distance = 0)
        {
            var wait = new WaitForFixedUpdate();
            for (int i = 0; i < 5; i++)
                yield return wait;
            while (vessel != null && (!vessel.loaded || vessel.packed))
                yield return wait;
            for (int i = 0; i < 5; i++)
                yield return wait;
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

            vessel.Parts[0].stackIcon.CreateIcon();
            StageManager.Instance.SortIcons(true);

            StageManager.ActivateNextStage();

            ModuleEnginesFX probeEngine = vessel.Parts[0].FindModuleImplementing<ModuleEnginesFX>();
            if (probeEngine != null)
            {
                probeEngine.Activate();
            }

            if (targetVessel != null)
                FlightGlobals.fetch.SetVesselTarget(targetVessel); FlightInputHandler.state.mainThrottle = tmpThrust / 100f;
        }

        public void ForceSwitchVessel(Vessel v)
        {

            if (v == null || !v.loaded)
                return;
            var camHeading = FlightCamera.CamHdg;
            var camPitch = FlightCamera.CamPitch;
            ReturnToObserver.instance.WaitForDestruct(v, FlightGlobals.ActiveVessel);
            FlightGlobals.ForceSetActiveVessel(v);
            FlightInputHandler.ResumeVesselCtrlState(v);
            FlightCamera.CamHdg = camHeading;
            FlightCamera.CamPitch = camPitch;

            RenameVessel(v, "Powered Impactor");
        }

        public static void RenameVessel(Vessel vessel, string newName)
        {
            if (vessel == null || string.IsNullOrEmpty(newName))
                return;

            string oldName = vessel.vesselName;

            // Update vessel name in memory
            vessel.vesselName = newName;

            // Update protoVessel so the name persists in saves
            if (vessel.protoVessel != null)
            {
                vessel.protoVessel.vesselName = newName;
            }
            // Fire the rename event so the map/tracking UI updates
            GameEvents.onVesselRename.Fire(
                new GameEvents.HostedFromToAction<Vessel, string>(vessel, oldName, newName)
            );
        }

        ConstructionEventType lastType = ConstructionEventType.Unknown;
        void onEditorPartEvent(ConstructionEventType t, Part part)
        {
            if (t != lastType)
            {
                lastType = t;
            }
            if (part.name == "sci-impact-probeslug")
            {
                if (nodesEnabled)
                    SetAttachNodes(false);
                UpdateEventFields();
            }
            else
            {
                if (!nodesEnabled)
                    SetAttachNodes(true);
            }
        }

        void SetAttachNodes(bool b)
        {
            Apply(b, true);
        }


        void EnableAttachNode(AttachNode node, int index)
        {
            if (node != null && !part.attachNodes.Contains(node))
            {
                if (index >= 0 && index <= part.attachNodes.Count)
                    part.attachNodes.Insert(index, node);
                else
                    part.attachNodes.Add(node);

                if (node.icon != null) node.icon.SetActive(true); // icon is a GameObject
            }
        }
        void DisableAttachNode(AttachNode node, int index, bool propagateSymmetry)
        {
            // If something is attached, optionally detach
            if (node.attachedPart == null)
            {
                index = part.attachNodes.IndexOf(node);
                if (node.icon != null)
                    node.icon.SetActive(false);
                part.attachNodes.Remove(node);
            }
            //else
            //    Log("node.attachedPart is not null");

            // Symmetry propagation in editor
            if (propagateSymmetry && HighLogic.LoadedSceneIsEditor && part.symmetryCounterparts != null)
            {
                foreach (var cp in part.symmetryCounterparts)
                {
                    var m = cp.FindModuleImplementing<ImpactNodeController>();
                    if (m != null)
                    {
                        m.Apply(false, false);
                    }
                }
            }
        }

        private void Apply(bool enable, bool propagateSymmetry)
        {
            nodesEnabled = enable;
            foreach (var w in nodeDict.Values)
            {
                var node = w.AttachNode;
                var index = w.index;

                if (enable)
                    EnableAttachNode(node, index);
                else
                    DisableAttachNode(node, index, propagateSymmetry);
            }

            foreach (var w in probeMountNodeDict.Values)
            {
                var node = w.AttachNode;
                var index = w.index;
                if (!enable)
                    EnableAttachNode(node, index);
                else
                    DisableAttachNode(node, index, propagateSymmetry);
            }
        }

    }
}
