﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MuMech
{
	public class MechJebModuleScriptActionKos : MechJebModuleScriptAction
	{
		public static String NAME = "kOS";
		private readonly List<Part> kosParts = new List<Part>();
		private readonly List<String> kosPartsNames = new List<String>();
		private readonly List<PartModule> kosModules = new List<PartModule>();
		[Persistent(pass = (int)Pass.Type)]
		private EditableInt selectedPartIndex = 0;
		[Persistent(pass = (int)Pass.Type)]
		private uint selectedPartFlightID = 0;
		[Persistent(pass = (int)Pass.Type)]
		private String command = "";
		[Persistent(pass = (int)Pass.Type)]
		private bool openTerminal = true;
		[Persistent(pass = (int)Pass.Type)]
		private bool waitFinish = true;
		[Persistent(pass = (int)Pass.Type)]
		private bool closeTerminal = true;
		private bool partHighlighted = false;
		//Reflected objects cache
		object sharedObjects = null;
		object interpreter = null;

		public MechJebModuleScriptActionKos (MechJebModuleScript scriptModule, MechJebCore core, MechJebModuleScriptActionsList actionsList):base(scriptModule, core, actionsList, NAME)
		{
			kosParts.Clear();
			kosPartsNames.Clear();
			kosModules.Clear();
			foreach (Vessel vessel in FlightGlobals.Vessels)
			{
				if (vessel.state != Vessel.State.DEAD)
				{
					foreach (Part part in vessel.Parts)
					{
						foreach (PartModule module in part.Modules)
						{
							if (module.moduleName.Contains("kOSProcessor"))
							{
								this.kosParts.Add(part);
								this.kosPartsNames.Add(part.partInfo.title);
								this.kosModules.Add(module);
							}
						}
					}
				}
			}
		}

		public override void activateAction()
		{
			base.activateAction();
			if (this.selectedPartIndex < this.kosModules.Count)
			{
				if (openTerminal)
				{
					this.kosModules[this.selectedPartIndex].GetType().InvokeMember("OpenWindow", System.Reflection.BindingFlags.InvokeMethod, null, this.kosModules[this.selectedPartIndex], null);
				}
				sharedObjects = this.kosModules[this.selectedPartIndex].GetType().GetField("shared", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(this.kosModules[this.selectedPartIndex]);
				if (sharedObjects != null)
				{
					interpreter = sharedObjects.GetType().GetProperty("Interpreter").GetValue(sharedObjects, null);
					if (interpreter != null)
					{
						interpreter.GetType().InvokeMember("ProcessCommand", System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, interpreter, new object[] { command });
						if (!this.waitFinish)
						{
							this.endAction();
						}
					}
					else
					{
						Debug.LogError("---- NO Interpreter OBJECT ----");
						this.endAction();
					}
				}
				else
				{
					Debug.LogError("---- NO SHARED OBJECT ----");
					this.endAction();
				}
			}
			else
			{
				this.endAction();
			}
		}

		public override  void endAction()
		{
			base.endAction();
			if (this.selectedPartIndex < this.kosModules.Count)
			{
				if (closeTerminal)
				{
					this.kosModules[this.selectedPartIndex].GetType().InvokeMember("CloseWindow", System.Reflection.BindingFlags.InvokeMethod, null, this.kosModules[this.selectedPartIndex], null);
				}
			}
		}

		public override void afterOnFixedUpdate()
		{
			//If we are waiting for the sequence to finish, we check the status
			if (!this.isExecuted() && this.isStarted())
			{
				if (isCPUActive(this.kosModules[this.selectedPartIndex]))
				{
					this.endAction();
				}
			}
		}

		public override void WindowGUI(int windowID)
		{
			base.preWindowGUI(windowID);
			base.WindowGUI(windowID);
			GUILayout.Label ("kOS");
			if (kosPartsNames.Count > 0)
			{
				selectedPartIndex = GuiUtils.ComboBox.Box(selectedPartIndex, kosPartsNames.ToArray(), kosPartsNames);
				if (!partHighlighted)
				{
					if (GUILayout.Button(GameDatabase.Instance.GetTexture("MechJeb2/Icons/view", true), GUILayout.ExpandWidth(false)))
					{
						partHighlighted = true;
						kosParts[selectedPartIndex].SetHighlight(true, false);
					}
				}
				else
				{
					if (GUILayout.Button(GameDatabase.Instance.GetTexture("MechJeb2/Icons/view_a", true), GUILayout.ExpandWidth(false)))
					{
						partHighlighted = false;
						kosParts[selectedPartIndex].SetHighlight(false, false);
					}
				}
				command = GUILayout.TextField(command, GUILayout.Width(120), GUILayout.ExpandWidth(true));
				openTerminal = GUILayout.Toggle(openTerminal, "Open Terminal");
				waitFinish = GUILayout.Toggle(waitFinish, "Wait Finish");
				closeTerminal = GUILayout.Toggle(closeTerminal, "Close Terminal");
			}
			else
			{
				GUILayout.Label("-- NO kOS module on vessel --");
			}
			if (selectedPartIndex < kosParts.Count)
			{
				this.selectedPartFlightID = kosParts[selectedPartIndex].flightID;
			}

			base.postWindowGUI(windowID);
		}

		public override void postLoad(ConfigNode node)
		{
			if (selectedPartFlightID != 0) //We check if a previous flightID was set on the parts. When switching MechJeb Cores and performing save/load of the script, the port order may change so we try to rely on the flight ID to select the right part.
			{
				int i = 0;
				foreach (Part part in kosParts)
				{
					if (part.flightID == selectedPartFlightID)
					{
						this.selectedPartIndex = i;
					}
					i++;
				}
			}
		}

		public bool isCPUActive(object module)
		{
			if (sharedObjects != null && interpreter != null)
			{
				//We check if the interpreter is waiting to know if our program has been executed
				bool waiting = (bool)interpreter.GetType().InvokeMember("IsWaitingForCommand", System.Reflection.BindingFlags.InvokeMethod, null, interpreter, null);
				if (waiting)
				{
					return true;
				}
			}
			return false;
		}
	}
}

