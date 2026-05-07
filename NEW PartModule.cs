using System;
using System.Linq;
using UnityEngine;

namespace YourMod
{
	public class AsteroidImpactSensor : PartModule
	{
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
		[KSPField] public string expSeis = "AST-SEIS";
		[KSPField] public string expEjecta = "AST-EJECTA";
		[KSPField] public string expVolatiles = "AST-VOLATILES";
		[KSPField] public string expMagdust = "AST-MAGDUST";
		[KSPField] public string expAlbedo = "AST-ALBEDO";
		[KSPField] public string expRingdown = "AST-RINGDOWN";

		// -------------------------------------------
		[KSPField(isPersistant = true)] public bool firedThisFrame = false;

		private Vector3d lastVelWorld = Vector3d.zero;

		public override void OnStart(StartState state)
		{
			base.OnStart(state);
			firedThisFrame = false;
		}

		public void FixedUpdate()
		{
			if (!HighLogic.LoadedSceneIsFlight || part?.rb == null) return;
			lastVelWorld = part.rb.GetPointVelocity(part.transform.position);
		}

		public void OnCollisionEnter(Collision c)
		{
			if (!HighLogic.LoadedSceneIsFlight) return;
			if (firedThisFrame) return;

			Part otherPart = c.collider.GetComponentUpwards<Part>();
			if (otherPart == null) return;

			bool hitAsteroid = otherPart.vessel != null &&
							   otherPart.vessel.parts.Any(p => p.FindModuleImplementing<ModuleAsteroid>() != null);
			if (!hitAsteroid) return;

			double impactSpeed = lastVelWorld.magnitude;
			if (impactSpeed < minImpactSpeed) return;

			Part asteroidPart = otherPart.vessel.parts.FirstOrDefault(p => p.FindModuleImplementing<ModuleAsteroid>() != null);
			double asteroidMass = asteroidPart != null ? asteroidPart.mass : 0;
			string asteroidClass = GuessAsteroidClassFromMass(asteroidMass);

			FireExperiments(impactSpeed, asteroidMass, asteroidClass);

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

		private void FireExperiments(double vImpact, double mAst, string aClass)
		{
			// keep your sweet-spot curves, then apply cfg multipliers inside TryRun()
			TryRun(expSeis, vImpact, aClass, ScoreBell(vImpact, 25, 15) + ScoreBell(vImpact, 100, 30), multSeis);
			TryRun(expEjecta, vImpact, aClass, Plateau(vImpact, 25, 200) * (1.0f + Mathf.Clamp01((float)(mAst / 300.0))), multEjecta);
			TryRun(expVolatiles, vImpact, aClass, ScoreBell(vImpact, 80, 25) * (aClass == "C" ? 1.3f : 1.0f), multVolatiles);
			TryRun(expMagdust, vImpact, aClass, ScoreBell(vImpact, 60, 22) * (aClass == "E" ? 1.25f : 1.0f), multMagdust);
			TryRun(expAlbedo, vImpact, aClass, Mathf.Clamp01((float)((vImpact - 10.0) / 60.0)), multAlbedo);
			TryRun(expRingdown, vImpact, aClass, ScoreBell(vImpact, 40, 18), multRingdown);
		}


		private void TryRun(string expID, double vImpact, string aClass, float quality, float expScalar)
		{
			if (quality <= 0.05f) return;

			var exp = ResearchAndDevelopment.GetExperiment(expID);
			if (exp == null)
			{
				ScreenMessages.PostScreenMessage($"[Impact] Experiment {expID} not found", 4, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			CelestialBody sun = FlightGlobals.Bodies.FirstOrDefault(b => b.isSun) ?? Planetarium.fetch.Sun;

			string biome = $"Asteroid {aClass}";
			var situation = ExperimentSituations.InSpaceLow;
			var subject = ResearchAndDevelopment.GetExperimentSubject(exp, situation, sun, biome, "");
			if (subject == null)
			{
				ScreenMessages.PostScreenMessage($"[Impact] Could not create subject for {expID}", 4, ScreenMessageStyle.UPPER_CENTER);
				return;
			}

			// Apply scalars: curve -> per-experiment -> class
			float classScalar = GetClassScalar(aClass);
			float scaledQuality = Mathf.Max(0.01f, quality) * Mathf.Max(0.01f, expScalar) * Mathf.Max(0.01f, classScalar);

			float baseUnits = Mathf.Clamp(scaledQuality * exp.baseValue, 0.1f, exp.scienceCap);

			var data = new ScienceData(
				baseUnits * exp.dataScale,
				xmitScalar: 0.6f,
				labScalar: 0.9f,
				id: expID,
				title: $"{exp.experimentTitle} — {biome}"
			);

			var container = part.FindModuleImplementing<ModuleScienceContainer>();
			if (container != null && container.AddData(data))
			{
				ScreenMessages.PostScreenMessage($"[Impact] Recorded {exp.experimentTitle} ({biome})", 5, ScreenMessageStyle.UPPER_LEFT);
			}
			else
			{
				ExperimentsResultDialog.DisplayResult(data, exp, 1f, true);
			}
		}

		private float GetClassScalar(string aClass)
		{
			if (string.IsNullOrEmpty(aClass)) return 1f;
			switch (aClass.Trim().ToUpperInvariant())
			{
				case "A": return Mathf.Max(0f, classMultA);
				case "B": return Mathf.Max(0f, classMultB);
				case "C": return Mathf.Max(0f, classMultC);
				case "D": return Mathf.Max(0f, classMultD);
				case "E": return Mathf.Max(0f, classMultE);
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
