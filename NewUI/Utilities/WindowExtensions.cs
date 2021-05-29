﻿using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mesen.Utilities
{
	static class WindowExtensions
	{
		private static void CenterWindow(Window child, Control parent)
		{
			child.WindowStartupLocation = WindowStartupLocation.Manual;
			Size wndCenter = new Size(parent.Bounds.Width / 2, parent.Bounds.Height / 2);
			PixelPoint controlPosition  =parent.PointToScreen(new Point(0, 0));
			PixelPoint screenCenter = new PixelPoint(controlPosition.X + (int)wndCenter.Width, controlPosition.Y + (int)wndCenter.Height);
			child.Position = new PixelPoint(screenCenter.X - (int)child.Width / 2, screenCenter.Y - (int)child.Height / 2);
		}

		public static void ShowCentered(this Window child, Control parent)
		{
			CenterWindow(child, parent);
			child.Show();
		}

		public static Task ShowCenteredDialog(this Window child, Control parent)
		{
			return ShowCenteredDialog<object>(child, parent);
		}

		public static Task<TResult> ShowCenteredDialog<TResult>(this Window child, Control parent)
		{
			CenterWindow(child, parent);

			IControl parentWnd = parent;
			while(!(parentWnd is Window) && parentWnd != null) {
				parentWnd = parentWnd.Parent;
			}

			return child.ShowDialog<TResult>(parentWnd as Window);
		}
	}
}
