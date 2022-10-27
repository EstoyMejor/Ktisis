using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;

using Dalamud.Interface;
using Dalamud.Interface.Components;

using FFXIVClientStructs.Havok;

using Ktisis.Structs.Bones;

namespace Ktisis.Util
{
	internal class GuiHelpers
	{
		public static bool IconButtonHoldConfirm(FontAwesomeIcon icon, string tooltip, bool isHoldingKey)
		{
			if (!isHoldingKey) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
			bool accepting = ImGuiComponents.IconButton(icon);
			if (!isHoldingKey) ImGui.PopStyleVar();

			Tooltip(tooltip);

			return accepting && isHoldingKey;
		}

		public static bool IconButtonTooltip(FontAwesomeIcon icon, string tooltip)
		{
			bool accepting = ImGuiComponents.IconButton(icon);
			Tooltip(tooltip);
			return accepting;
		}
		public static bool TextButtonTooltip(string label, string tooltip)
		{
			bool accepting = ImGui.Button(label);
			Tooltip(tooltip);
			return accepting;
		}
		public static void TextTooltip(string label, string tooltip)
		{
			ImGui.Text(label);
			Tooltip(tooltip);
		}
		public static void TextDisabledTooltip(string label, string tooltip)
		{
			ImGui.TextDisabled(label);
			Tooltip(tooltip);
		}

		public static void Tooltip(string text)
		{
			if (ImGui.IsItemHovered())
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
				ImGui.TextUnformatted(text);
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
			}
		}
		public static void PopupConfirm(string label, Action contents, Action onAccept)
		{
			ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new Vector2(0.5f));
			if (ImGui.BeginPopup(label, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
			{
				contents();
				ImGui.Separator();

				var okSize = new Vector2(120, 0) * ImGui.GetIO().FontGlobalScale;
				var cancelSize = new Vector2(120, 0) * ImGui.GetIO().FontGlobalScale;

				var buttonSize = okSize.X + cancelSize.X;
				var buttonCenter = (ImGui.GetWindowContentRegionMax().X - buttonSize) / 2;

				ImGui.SetCursorPosX(buttonCenter);
				if (ImGui.Button("OK", okSize))
				{
					ImGui.CloseCurrentPopup();
					onAccept();
				}

				ImGui.SetItemDefaultFocus();
				ImGui.SameLine();
				if (ImGui.Button("Cancel", cancelSize))
				{
					ImGui.CloseCurrentPopup();
				}

				ImGui.EndPopup();
			}
		}

		public static bool DrawBoneNode(Bone bone, ImGuiTreeNodeFlags flag, System.Action? executeIfClicked = null)
		{
			bool show = ImGui.TreeNodeEx(bone.UniqueId, flag, bone.LocaleName);

			var rectMin = ImGui.GetItemRectMin() + new Vector2(ImGui.GetTreeNodeToLabelSpacing(), 0);
			var rectMax = ImGui.GetItemRectMax();

			var mousePos = ImGui.GetMousePos();
			if (
				ImGui.IsMouseClicked(ImGuiMouseButton.Left)
				&& mousePos.X > rectMin.X && mousePos.X < rectMax.X
				&& mousePos.Y > rectMin.Y && mousePos.Y < rectMax.Y
			)
			{
				executeIfClicked?.Invoke();
			}
			return show;
		}

		public static void TextRight(string text, float offset = 0)
		{
			offset = ImGui.GetContentRegionAvail().X - offset - ImGui.CalcTextSize(text).X;
			ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
			ImGui.TextUnformatted(text);
		}

		public static void TextCentered(string text)
		{
			var windowWidth = ImGui.GetWindowSize().X;
			var textWidth = ImGui.CalcTextSize(text).X;

			ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
			ImGui.Text(text);
		}
		
		public static unsafe void AnimationControls(hkaDefaultAnimationControl* control)
		{
			var duration = control->hkaAnimationControl.Binding.ptr->Animation.ptr->Duration;
			var durationLimit = duration - 0.05f;
			
			if (control->hkaAnimationControl.LocalTime >= durationLimit)
				control->hkaAnimationControl.LocalTime = 0f;
        
			ImGui.SliderFloat("Seek", ref control->hkaAnimationControl.LocalTime, 0, durationLimit);
			ImGui.SliderFloat("Speed", ref control->PlaybackSpeed, 0f, 0.999f);
		}

		// HoverPopupWindow Method
		// Constants
		private const ImGuiKey KeyBindBrowseUp = ImGuiKey.UpArrow;
		private const ImGuiKey KeyBindBrowseDown = ImGuiKey.DownArrow;
		private const ImGuiKey KeyBindBrowseLeft = ImGuiKey.LeftArrow;
		private const ImGuiKey KeyBindBrowseRight = ImGuiKey.RightArrow;
		private const ImGuiKey KeyBindBrowseUpFast = ImGuiKey.PageUp;
		private const ImGuiKey KeyBindBrowseDownFast = ImGuiKey.PageDown;
		private const int HoverPopupWindowFastScrollLineJump = 8; // number of lines on the screen?

		// Properties
		private static Vector2 HoverPopupWindowSelectPos = Vector2.Zero;
		private static bool HoverPopupWindowIsBegan = false;
		private static bool HoverPopupWindowFocus = false;
		private static bool HoverPopupWindowSearchBarValidated = false;
		public static int HoverPopupWindowLastSelectedItemKey = 0;
		public static int HoverPopupWindowColumns = 1;
		private static Action? PreviousOnClose;
		public static int HoverPopupWindowIndexKey = 0;
		public static dynamic? HoverPopupWindowItemForHeader = null;

		[Flags]
		public enum HoverPopupWindowFlags
		{
			None = 0,
			SelectorList = 1,
			SearchBar = 2,
			Grabbable = 4,
			TwoDimenssion = 8,
			Header = 16,
			StayWhenLoseFocus = 32, // TODO: make it instanciable so we can have multiple
		}
		private static int RowFromKey(int key) => (int)Math.Floor((double)(key / HoverPopupWindowColumns));
		private static int ColFromKey(int key) => key % HoverPopupWindowColumns;
		private static int KeyFromRowCol(int row, int col) => (row * HoverPopupWindowColumns) + col;

		public static void HoverPopupWindow(
				HoverPopupWindowFlags flags,
				IEnumerable<dynamic> enumerable,
				Func<IEnumerable<dynamic>, string, IEnumerable<dynamic>> filter,
				Action<dynamic> header,
				Func<dynamic, bool, (bool,bool)> drawBeforeLine, // Parameters: dynamic item, bool isActive. Returns bool isSelected, bool Focus.
				Action<dynamic> onSelect,
				Action onClose,
				ref string inputSearch,
				string windowLabel = "",
				string listLabel = "",
				string searchBarLabel = "##search",
				string searchBarHint = "Search...",
				float minWidth = 400,
				int columns = 12
		)
		{
			PreviousOnClose ??= onClose;
			if (onClose != PreviousOnClose)
			{
				// for StayWhenLoseFocus, close
				PreviousOnClose();
				PreviousOnClose = onClose;
				HoverPopupWindowSelectPos = Vector2.Zero;
			}
			HoverPopupWindowColumns = columns;

			var size = new Vector2(-1, -1);
			ImGui.SetNextWindowSize(size, ImGuiCond.Always);

			bool isNewPop = HoverPopupWindowSelectPos == Vector2.Zero;
			if (isNewPop) HoverPopupWindowSelectPos = ImGui.GetMousePos();
			if (!flags.HasFlag(HoverPopupWindowFlags.Grabbable) || isNewPop)
				ImGui.SetNextWindowPos(HoverPopupWindowSelectPos);

			ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));

			ImGuiWindowFlags windowFlags = ImGuiWindowFlags.None;
			if (!flags.HasFlag(HoverPopupWindowFlags.Grabbable))
				windowFlags |= ImGuiWindowFlags.NoDecoration;

			HoverPopupWindowIsBegan = ImGui.Begin(windowLabel, windowFlags);
			if (HoverPopupWindowIsBegan)
			{
				HoverPopupWindowFocus = ImGui.IsWindowFocused() || ImGui.IsWindowHovered();
				ImGui.PushItemWidth(minWidth);
				if (flags.HasFlag(HoverPopupWindowFlags.SearchBar))
					HoverPopupWindowSearchBarValidated = ImGui.InputTextWithHint(searchBarLabel, searchBarHint, ref inputSearch, 32, ImGuiInputTextFlags.EnterReturnsTrue);

				if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
					ImGui.SetKeyboardFocusHere(flags.HasFlag(HoverPopupWindowFlags.SearchBar) ? -1 : 0); // TODO: verify the keyboarf focus behaviour when searchbar is disabled

				if (flags.HasFlag(HoverPopupWindowFlags.SelectorList))
					ImGui.BeginListBox(listLabel, new Vector2(-1, 300));
				// box has began

				if (flags.HasFlag(HoverPopupWindowFlags.Header))
				{
					if (HoverPopupWindowItemForHeader != null)
						header(HoverPopupWindowItemForHeader);
					else
						ImGui.Text("");
				}

				if (flags.HasFlag(HoverPopupWindowFlags.SearchBar)) {
					if (inputSearch.Length > 0) {
						var inputSearch2 = inputSearch;
						enumerable = filter(enumerable, inputSearch2);
					}
				}

				HoverPopupWindowIndexKey = 0;
				bool isOneSelected = false; // allows one selection per foreach
				if (!flags.HasFlag(HoverPopupWindowFlags.TwoDimenssion))
					if (HoverPopupWindowLastSelectedItemKey >= enumerable.Count()) HoverPopupWindowLastSelectedItemKey = enumerable.Count() - 1;

				foreach (var i in enumerable)
				{
					bool selecting = false;
					bool isCurrentActive = HoverPopupWindowIndexKey == HoverPopupWindowLastSelectedItemKey;

					var drawnLineTurpe = drawBeforeLine(i, isCurrentActive);
					HoverPopupWindowFocus |= ImGui.IsItemFocused();
					selecting |= drawnLineTurpe.Item1;
					HoverPopupWindowFocus |= drawnLineTurpe.Item2;

					if (!isOneSelected)
					{
						if (flags.HasFlag(HoverPopupWindowFlags.TwoDimenssion))
						{
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseUp) && RowFromKey(HoverPopupWindowIndexKey) == RowFromKey(HoverPopupWindowLastSelectedItemKey) - 1 && ColFromKey(HoverPopupWindowIndexKey) == ColFromKey(HoverPopupWindowLastSelectedItemKey);
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseDown) && RowFromKey(HoverPopupWindowIndexKey) == RowFromKey(HoverPopupWindowLastSelectedItemKey) + 1 && ColFromKey(HoverPopupWindowIndexKey) == ColFromKey(HoverPopupWindowLastSelectedItemKey);
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseUpFast) && RowFromKey(HoverPopupWindowIndexKey) == RowFromKey(HoverPopupWindowLastSelectedItemKey) - HoverPopupWindowFastScrollLineJump && ColFromKey(HoverPopupWindowIndexKey) == ColFromKey(HoverPopupWindowLastSelectedItemKey);
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseDownFast) && RowFromKey(HoverPopupWindowIndexKey) == RowFromKey(HoverPopupWindowLastSelectedItemKey) + HoverPopupWindowFastScrollLineJump && ColFromKey(HoverPopupWindowIndexKey) == ColFromKey(HoverPopupWindowLastSelectedItemKey);
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseLeft) && ColFromKey(HoverPopupWindowIndexKey) == ColFromKey(HoverPopupWindowLastSelectedItemKey) - 1 && RowFromKey(HoverPopupWindowIndexKey) == RowFromKey(HoverPopupWindowLastSelectedItemKey);
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseRight) && ColFromKey(HoverPopupWindowIndexKey) == ColFromKey(HoverPopupWindowLastSelectedItemKey) + 1 && RowFromKey(HoverPopupWindowIndexKey) == RowFromKey(HoverPopupWindowLastSelectedItemKey);
						}
						else
						{
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseUp) && HoverPopupWindowIndexKey == HoverPopupWindowLastSelectedItemKey - 1;
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseDown) && HoverPopupWindowIndexKey == HoverPopupWindowLastSelectedItemKey + 1;
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseUpFast) && HoverPopupWindowIndexKey == HoverPopupWindowLastSelectedItemKey - HoverPopupWindowFastScrollLineJump;
							selecting |= ImGui.IsKeyPressed(KeyBindBrowseDownFast) && HoverPopupWindowIndexKey == HoverPopupWindowLastSelectedItemKey + HoverPopupWindowFastScrollLineJump;
						}
						selecting |= HoverPopupWindowSearchBarValidated;
					}

					if (selecting)
					{
						if (ImGui.IsKeyPressed(KeyBindBrowseUp) || ImGui.IsKeyPressed(KeyBindBrowseDown) || ImGui.IsKeyPressed(KeyBindBrowseUpFast) || ImGui.IsKeyPressed(KeyBindBrowseDownFast))
							ImGui.SetScrollY(ImGui.GetCursorPosY() - (ImGui.GetWindowHeight() / 2));

						onSelect(i);
						// assigning cache vars
						HoverPopupWindowLastSelectedItemKey = HoverPopupWindowIndexKey;
						isOneSelected = true;
						HoverPopupWindowItemForHeader = i;
					}
					HoverPopupWindowFocus |= ImGui.IsItemFocused();
					HoverPopupWindowIndexKey++;
				}


				// box has ended
				if (flags.HasFlag(HoverPopupWindowFlags.SelectorList))
					ImGui.EndListBox();
				HoverPopupWindowFocus |= ImGui.IsItemActive();
				ImGui.PopItemWidth();

				if ((!flags.HasFlag(HoverPopupWindowFlags.StayWhenLoseFocus) && !HoverPopupWindowFocus) || ImGui.IsKeyPressed(ImGuiKey.Escape))
				{
					onClose();

					// cleaning cache vars
					PreviousOnClose = null;
					HoverPopupWindowSelectPos = Vector2.Zero;
					HoverPopupWindowIndexKey = 0;
					HoverPopupWindowItemForHeader = null;
				}
			}

			ImGui.End();
		}
	}
}