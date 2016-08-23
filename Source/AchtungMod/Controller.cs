﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AchtungMod
{
	public class Controller
	{
		List<Colonist> colonists;
		Vector3 lineStart;
		Vector3 lineEnd;
		bool isDragging;
		bool shiftPositions;
		bool drawColonistPreviews;

		public static Controller controller = null;
		public static Controller getInstance()
		{
			if (controller == null) controller = new Controller();
			return controller;
		}

		public Controller()
		{
			colonists = new List<Colonist>();
			lineStart = Vector3.zero;
			lineEnd = Vector3.zero;
			isDragging = false;
			drawColonistPreviews = true;
		}

		public void MouseDown(Vector3 pos)
		{
			if (Event.current.button == 1)
			{
				Vector3 where = Gen.MouseMapPosVector3();

				bool altKeyPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
				shiftPositions = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

				colonists = Tools.UserSelectedAndReadyPawns().Select(p => new Colonist(p, altKeyPressed)).ToList();
				if (colonists.Count > 0)
				{
					if (colonists.Count == 1 && altKeyPressed == false)
					{
						List<FloatMenuOption> choices = FloatMenuMakerMap.ChoicesAtFor(where, colonists.First().pawn);
						if (choices.Count > 0)
						{
							// don't overwrite existing floating menu
							return;
						}
					}

					// build multi menu from existing commands
					MultiActions actions = new MultiActions(colonists, where);

					// present combined menu to the user
					if (actions.Count() > 0 && altKeyPressed == false)
					{
						Find.WindowStack.Add(actions.GetWindow());
						Event.current.Use();
						return;
					}

					if (colonists.Count == 1 && Tools.GetDraftingStatus(colonists.First().pawn) == false)
					{
						// don't drag if neither standard menu nor multi menu have any choices and it's a single colonist
						return;
					}

					// start dragging
					lineStart = pos;
					lineStart.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);

					isDragging = true;
					Event.current.Use();
				}
			}
		}

		public void MouseDrag(Vector3 pos)
		{
			if (isDragging == true)
			{
				lineEnd = pos;
				lineEnd.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
				int count = colonists.Count;
				Vector3 dragVector = lineEnd - lineStart;

				if (shiftPositions)
				{
					colonists.ForEach(colonist =>
					{
						Vector3 delta = lineEnd - lineStart;
						colonist.OrderTo(colonist.startPosition + delta);
					});
				}
				else
				{
					Vector3 delta = count > 1 ? dragVector / (float)(count - 1) : Vector3.zero;
					Vector3 linePosition = count == 1 ? lineEnd : lineStart;
					colonists.ForEach(colonist =>
					{
						colonist.OrderTo(linePosition);
						linePosition += delta;
					});
				}

				Event.current.Use();
			}
		}

		public List<FloatMenuOption> ChoicesAtFor(Vector3 clickPos, Pawn pawn)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();

			bool altKeyPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
			if (altKeyPressed == false)
			{
				if (JobDriver_FightFire.CanStart(pawn, clickPos.ToIntVec3()))
				{
					Action action = delegate
					{
						JobDriver_FightFire.StartJob(pawn, clickPos.ToIntVec3());
					};
					options.Add(new FloatMenuOption("FightThisFire".Translate(), action, MenuOptionPriority.Low));
				}

				RoomInfo info = JobDriver_CleanRoom.CanStart(pawn, clickPos.ToIntVec3());
				if (info.valid && info.room != null)
				{
					Action action = delegate
						{
							JobDriver_CleanRoom.StartJob(pawn, clickPos.ToIntVec3());
						};
					options.Add(new FloatMenuOption("CleanThisRoom".Translate(), action, MenuOptionPriority.Low));
				}
				// else if (info.room != null && info.room.Role != RoomRoleDefOf.None)
				// {
				// 	string error = info.room.Role.label + ": " + info.error;
				// FloatMenuOption option = new FloatMenuOption("CleanThisRoom".Translate() + "(" + info.error + ")", null, MenuOptionPriority.Low);
				// option.Disabled = true;
				// options.Add(option);
				// }
			}

			return options;
		}

		public void MouseUp(Vector3 pos)
		{
			if (isDragging == true)
			{
				colonists.Clear();
				Event.current.Use();
			}
			isDragging = false;
		}

		public void KeyDown(KeyCode key)
		{
			if (isDragging == true)
			{
				if (key == KeyCode.Escape)
				{
					isDragging = false;

					colonists.ForEach(colonist =>
					{
						Tools.SetDraftStatus(colonist.pawn, colonist.originalDraftStatus);
						colonist.pawn.mindState.priorityWork.Clear();
						if ((colonist.pawn.jobs.curJob != null) && colonist.pawn.drafter.CanTakeOrderedJob())
						{
							colonist.pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
						}
					});

					colonists.Clear();
					Event.current.Use();
				}
			}
		}

		public void HandleDrawing()
		{
			if (isDragging)
			{
				if (colonists.Count > 1) Tools.DrawLineBetween(lineStart, lineEnd, 1.0f);

				colonists.ForEach(colonist =>
				{
					if (colonist.designation != Vector3.zero)
					{
						Tools.DrawMarker(colonist.designation);

						if (drawColonistPreviews)
						{
							colonist.pawn.Drawer.renderer.RenderPawnAt(colonist.designation);
							colonist.pawn.DrawExtraSelectionOverlays();
						}
					}
				});
			}
		}

		public void HandleDrawingOnGUI()
		{
			colonists.ForEach(colonist =>
			{
				if (colonist.designation != Vector3.zero)
				{
					Vector2 labelPos = Tools.LabelDrawPosFor(colonist.designation, -0.6f);
					GenWorldUI.DrawPawnLabel(colonist.pawn, labelPos, 1f, 9999f, null);
				}
			});
		}

		public void HandleEvents()
		{
			Vector3 pos = Gen.MouseMapPosVector3();
			switch (Event.current.type)
			{
				case EventType.mouseDown:
					MouseDown(pos);
					MouseDrag(pos);
					break;
				case EventType.MouseDrag:
					MouseDrag(pos);
					break;
				case EventType.mouseUp:
					MouseUp(pos);
					break;
				case EventType.KeyDown:
					KeyDown(Event.current.keyCode);
					break;
				default:
					break;
			}
		}
	}
}