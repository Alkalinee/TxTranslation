﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TxEditor.ViewModel;
using Unclassified;
using Unclassified.UI;

namespace TxEditor.View
{
	public partial class MainWindow : Window
	{
		#region Static constructor

		static MainWindow()
		{
			ViewCommandManager.SetupMetadata<MainWindow>();
		}

		#endregion Static constructor

		#region Static data

		public static MainWindow Instance { get; private set; }

		#endregion Static data

		#region Private data

		/// <summary>
		/// Reference to all HotKey instances to prevent garbage collection.
		/// </summary>
		private List<HotKey> hotKeys = new List<HotKey>();

		#endregion Private data

		#region Constructors

		public MainWindow()
		{
			Instance = this;
			
			InitializeComponent();

			WindowStartupLocation = WindowStartupLocation.Manual;
			Left = App.Settings.GetInt("window.left", (int) SystemParameters.WorkArea.Left + 20);
			Top = App.Settings.GetInt("window.top", (int) SystemParameters.WorkArea.Top + 20);
			Width = App.Settings.GetInt("window.width", 950);
			Height = App.Settings.GetInt("window.height", 600);
			WindowState = (WindowState) App.Settings.GetInt("window.state", (int) WindowState.Normal);
		}

		#endregion Constructors

		#region Window event handlers

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			hotKeys.Add(new HotKey(Key.T, HotKeyModifier.Ctrl | HotKeyModifier.Shift, OnHotKey));

			TextKeysTreeView.Focus();
		}

		private void OnHotKey(HotKey hotKey)
		{
			var vm = DataContext as MainWindowViewModel;
			if (vm != null)
			{
				vm.TextKeyWizardFromHotKey();
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			MainWindowViewModel vm = DataContext as MainWindowViewModel;
			if (vm != null && !vm.CheckModifiedSaved())
			{
				e.Cancel = true;
				return;
			}
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			App.Settings.SaveNow();
		}

		private void Window_LocationChanged(object sender, EventArgs e)
		{
			if (App.Settings != null)
			{
				App.Settings.Set("window.left", (int) RestoreBounds.Left);
				App.Settings.Set("window.top", (int) RestoreBounds.Top);
				App.Settings.Set("window.width", (int) RestoreBounds.Width);
				App.Settings.Set("window.height", (int) RestoreBounds.Height);
				App.Settings.Set("window.state", (int) WindowState);
			}
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			Window_LocationChanged(this, EventArgs.Empty);
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
			{
				SearchText.Focus();
			}
			if (e.Key == Key.Escape && e.KeyboardDevice.Modifiers == ModifierKeys.None)
			{
				if (SearchText.IsKeyboardFocused)
				{
					SearchText.Text = "";
				}
				TextKeysTreeView.Focus();
			}
			if (e.Key == Key.F2 && e.KeyboardDevice.Modifiers == ModifierKeys.None)
			{
				(DataContext as MainWindowViewModel).RenameTextKeyCommand.TryExecute();
			}
		}

		#endregion Window event handlers

		#region Tool grid layouting

		private bool collapsingItems;
		private double lastToolGridWidth;

		private void InnerToolGrid_LayoutUpdated(object sender, EventArgs e)
		{
			// NOTE: The collapsing method is only called when the layout was updated after a
			// change in the window width. Other layout events include resizing of tool buttons
			// (when changing the button text), but also click events on some buttons.
			// ToggleButtons fail when they and the next collapse priority have been collapsed.
			// As a work-around, no further UpdateLayout (and hence also no AutoCollapseItems)
			// call shall be made if the window size has not changed.
			if (ToolGrid.ActualWidth != lastToolGridWidth)
			{
				lastToolGridWidth = ToolGrid.ActualWidth;
				AutoCollapseItems();
			}
		}

		private void AutoCollapseItems()
		{
			if (collapsingItems) return;
			if (ToolGrid.ActualWidth < 10) return;   // Something is wrong, maybe the window is minimised?

			// Prevent further calls before the layouting is completely finished
			collapsingItems = true;
			Dispatcher.BeginInvoke((Action) (() => { collapsingItems = false; }), System.Windows.Threading.DispatcherPriority.Loaded);

			// Collapse toolbar items in their specified priority to save space until all items
			// fit in the toolbar. When collapsing, the item's display style is reduced from
			// image and text to image-only. This is only applied to items with a specified
			// collapse priority.

			// Restore the display style of all items that have a collpase priority.
			var items = new List<ICollapsableToolbarItem>();
			EnumCollapsableItems(InnerToolGrid, items);
			Dictionary<ICollapsableToolbarItem, int> collapsePriorities = new Dictionary<ICollapsableToolbarItem, int>();
			foreach (var item in items)
			{
				if (item.CollapsePriority > 0)
				{
					item.ContentVisibility = Visibility.Visible;
					collapsePriorities[item] = item.CollapsePriority;
				}
			}

			// Find the width of the right-most separator if there is one.
			// This width can be subtracted from the toolbar's preferred width because the last
			// separator doesn't need to be shown.
			// TODO
			//int extentWidth = 0;
			//var lastItem = Items.OfType<ToolStripItem>().OrderByDescending(i => GetColumn(i)).FirstOrDefault();
			//if (lastItem is ToolStripSeparator)
			//{
			//    extentWidth = lastItem.Width;
			//}
			int extentWidth = 6;

			// Group all items by their descending collapse priority and set their display style
			// to image-only as long as all items don't fit in the toolbar.
			var itemGroups =
				from kvp in collapsePriorities
				where kvp.Value > 0
				group kvp by kvp.Value into g
				orderby g.Key descending
				select g;
			foreach (var grp in itemGroups)
			{
				InnerToolGrid.UpdateLayout();
				if (InnerToolGrid.RenderSize.Width - extentWidth <= ToolGrid.ActualWidth) break;
				foreach (var kvp in grp)
				{
					kvp.Key.ContentVisibility = Visibility.Collapsed;
				}
			}
		}

		private void EnumCollapsableItems(Panel root, List<ICollapsableToolbarItem> items)
		{
			foreach (var child in root.Children)
			{
				ICollapsableToolbarItem item = child as ICollapsableToolbarItem;
				if (item != null)
				{
					items.Add(item);
					continue;
				}
				Panel childPanel = child as Panel;
				if (childPanel != null)
				{
					EnumCollapsableItems(childPanel, items);
				}
			}
		}

		#endregion Tool grid layouting

		#region Toolbar event handlers

		private void CultureToolsButton_Click(object sender, RoutedEventArgs e)
		{
			CultureToolsButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			CultureToolsButton.ContextMenu.PlacementTarget = CultureToolsButton;
			CultureToolsButton.ContextMenu.IsOpen = true;
		}

		private void CharMapButton_ToolTipOpening(object sender, ToolTipEventArgs e)
		{
			FrameworkElement obj = sender as FrameworkElement;
			if (obj != null)
			{
				ToolTipService.SetPlacement(obj, System.Windows.Controls.Primitives.PlacementMode.Relative);
				ToolTipService.SetVerticalOffset(obj, obj.ActualHeight + 6);
			}
		}

		private void CharmapButton_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			string text = button.Content as string;

			var target = Keyboard.FocusedElement;
			var routedEvent = TextCompositionManager.TextInputEvent;

			target.RaiseEvent(new TextCompositionEventArgs(
				InputManager.Current.PrimaryKeyboardDevice,
				new TextComposition(InputManager.Current, target, text)) { RoutedEvent = routedEvent }
			);
		}

		#endregion Toolbar event handlers

		#region Tree event handlers

		private void TextKeysTreeView_SelectionChanged(object sender, EventArgs e)
		{
			var vm = DataContext as MainWindowViewModel;
			if (vm != null)
			{
				vm.TextKeySelectionChanged(TextKeysTreeView.SelectedItems);
			}
		}

		private void TextKeysTreeView_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Delete && e.KeyboardDevice.Modifiers == 0)
			{
				(DataContext as MainWindowViewModel).DeleteTextKeyCommand.TryExecute();
				e.Handled = true;
			}
		}

		#endregion Tree event handlers

		#region View commands

		[ViewCommand]
		public void SelectTextKey(object textKey)
		{
			TextKeysTreeView.SelectedItems.Clear();
			if (textKey != null)
			{
				TextKeysTreeView.SelectedItems.Add(textKey);
				TextKeysTreeView.FocusItem(textKey, true);
			}
			else if (TextKeysTreeView.Items.Count > 0)
			{
				TextKeysTreeView.FocusItem(TextKeysTreeView.Items[0], true);
			}
			else
			{
				TextKeysTreeView.Focus();
			}
		}

		#endregion View commands
	}
}