﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Mesen.Config;
using Mesen.Debugger.Controls;
using Mesen.Debugger.Utilities;
using Mesen.Interop;
using Mesen.Localization;
using Mesen.Utilities;
using Mesen.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using System.Reflection;

namespace Mesen.Debugger.ViewModels
{
	public class EventViewerViewModel : DisposableViewModel
	{
		public const int HdmaChannelFlag = 0x40;

		[Reactive] public CpuType CpuType { get; set; }
		[Reactive] public DynamicBitmap ViewerBitmap { get; private set; }
		[Reactive] public EventViewerTab SelectedTab { get; set; }

		//Used to speed up display of the DataBox when clicking on the list tab
		//Faster to resize the list and always keep it mounted, rather than mounting it when tab is clicked
		[Reactive] public bool ShowViewer { get; set; } = true;
		[Reactive] public double ListHeight { get; set; } = 0;
		
		[Reactive] public ViewModelBase ConsoleConfig { get; set; }
		[Reactive] public GridRowColumn? GridHighlightPoint { get; set; }
		
		public EventViewerListViewModel ListView { get; }

		public EventViewerConfig Config { get; }
		
		[Reactive] public List<object> FileMenuItems { get; private set; } = new();
		[Reactive] public List<ContextMenuAction> DebugMenuItems { get; private set; } = new();
		[Reactive] public List<object> ViewMenuItems { get; private set; } = new();

		[Reactive] public List<ContextMenuAction> ToolbarItems { get; private set; } = new();

		private PictureViewer _picViewer;

		[Obsolete("For designer only")]
		public EventViewerViewModel() : this(CpuType.Nes, new PictureViewer(), null) { }

		public EventViewerViewModel(CpuType cpuType, PictureViewer picViewer, Window? wnd)
		{
			CpuType = cpuType;
			ListView = new EventViewerListViewModel(this);

			_picViewer = picViewer;
			Config = ConfigManager.Config.Debug.EventViewer;
			InitBitmap(new FrameInfo() { Width = 1, Height = 1 });
			InitForCpuType();

			FileMenuItems = AddDisposables(new List<object>() {
				new ContextMenuAction() {
					ActionType = ActionType.Exit,
					OnClick = () => wnd?.Close()
				}
			});

			ViewMenuItems = AddDisposables(new List<object>() {
				new ContextMenuAction() {
					ActionType = ActionType.Refresh,
					Shortcut = () => ConfigManager.Config.Debug.Shortcuts.Get(DebuggerShortcut.Refresh),
					OnClick = () => RefreshData()
				},
				new ContextMenuSeparator(),
				new ContextMenuAction() {
					ActionType = ActionType.EnableAutoRefresh,
					IsSelected = () => Config.AutoRefresh,
					OnClick = () => Config.AutoRefresh = !Config.AutoRefresh
				},
				new ContextMenuAction() {
					ActionType = ActionType.RefreshOnBreakPause,
					IsSelected = () => Config.RefreshOnBreakPause,
					OnClick = () => Config.RefreshOnBreakPause = !Config.RefreshOnBreakPause
				},
				new ContextMenuSeparator(),
				new ContextMenuAction() {
					ActionType = ActionType.ShowSettingsPanel,
					Shortcut =  () => ConfigManager.Config.Debug.Shortcuts.Get(DebuggerShortcut.ToggleSettingsPanel),
					IsSelected = () => Config.ShowSettingsPanel,
					OnClick = () => Config.ShowSettingsPanel = !Config.ShowSettingsPanel
				},
				new ContextMenuAction() {
					ActionType = ActionType.ShowToolbar,
					IsSelected = () => Config.ShowToolbar,
					OnClick = () => Config.ShowToolbar = !Config.ShowToolbar
				},
				new ContextMenuSeparator(),
				new ContextMenuAction() {
					ActionType = ActionType.ZoomIn,
					Shortcut = () => ConfigManager.Config.Debug.Shortcuts.Get(DebuggerShortcut.ZoomIn),
					OnClick = () => _picViewer.ZoomIn()
				},
				new ContextMenuAction() {
					ActionType = ActionType.ZoomOut,
					Shortcut = () => ConfigManager.Config.Debug.Shortcuts.Get(DebuggerShortcut.ZoomOut),
					OnClick = () => _picViewer.ZoomOut()
				},
			});

			if(Design.IsDesignMode || wnd == null) {
				return;
			}

			UpdateConfig();
			RefreshData();

			DebugMenuItems = AddDisposables(DebugSharedActions.GetStepActions(wnd, () => CpuType));
			ToolbarItems = AddDisposables(DebugSharedActions.GetStepActions(wnd, () => CpuType));

			AddDisposable(this.WhenAnyValue(x => x.CpuType).Subscribe(_ => {
				InitForCpuType();
				UpdateConfig();
				RefreshData();
			}));

			AddDisposable(this.WhenAnyValue(x => x.SelectedTab).Subscribe(x => RefreshTab()));
			
			AddDisposable(ReactiveHelper.RegisterRecursiveObserver(Config, (s, e) => {
				UpdateConfig();
				RefreshTab();
			}));

			DebugShortcutManager.RegisterActions(wnd, FileMenuItems);
			DebugShortcutManager.RegisterActions(wnd, DebugMenuItems);
			DebugShortcutManager.RegisterActions(wnd, ViewMenuItems);
		}

		[MemberNotNull(nameof(EventViewerViewModel.ConsoleConfig))]
		private void InitForCpuType()
		{
			ConsoleConfig = CpuType switch {
				CpuType.Snes => Config.SnesConfig,
				CpuType.Nes => Config.NesConfig,
				CpuType.Gameboy => Config.GbConfig,
				CpuType.Pce => Config.PceConfig,
				_ => throw new Exception("Invalid cpu type")
			};
		}

		private void InitBitmap()
		{
			InitBitmap(DebugApi.GetEventViewerDisplaySize(CpuType));
		}

		[MemberNotNull(nameof(ViewerBitmap))]
		private void InitBitmap(FrameInfo size)
		{
			if(ViewerBitmap == null || ViewerBitmap.Size.Width != size.Width || ViewerBitmap.Size.Height != size.Height) {
				ViewerBitmap = new DynamicBitmap(new PixelSize((int)size.Width, (int)size.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
			}
		}

		public void RefreshData(bool forAutoRefresh = false)
		{
			DebugApi.TakeEventSnapshot(CpuType, forAutoRefresh);
			RefreshTab();
		}

		public void RefreshTab()
		{
			Dispatcher.UIThread.Post(() => {
				if(SelectedTab == EventViewerTab.PpuView) {
					ListHeight = 0;
					ShowViewer = true;
					InitBitmap();
					using(var bitmapLock = ViewerBitmap.Lock()) {
						DebugApi.GetEventViewerOutput(CpuType, bitmapLock.FrameBuffer.Address, (uint)(ViewerBitmap.Size.Width * ViewerBitmap.Size.Height * sizeof(UInt32)));
					}
				} else {
					ShowViewer = false;
					ListHeight = double.NaN;
					ListView.RefreshList();
				}
			});
		}

		public void UpdateHighlightPoint(PixelPoint p, DebugEventInfo? eventInfo)
		{
			if(eventInfo != null) {
				//Snap the row/column highlight to the selected event
				DebugEventInfo evt = eventInfo.Value;
				p = CpuType switch {
					CpuType.Snes => new PixelPoint(evt.Cycle / 2, evt.Scanline * 2),
					CpuType.Nes => new PixelPoint(evt.Cycle * 2, (evt.Scanline + 1) * 2),
					CpuType.Gameboy => new PixelPoint(evt.Cycle * 2, evt.Scanline * 2),
					CpuType.Pce => new PixelPoint(evt.Cycle, evt.Scanline * 2),
					_ => throw new Exception("Invalid cpu type")
				};
			}

			GridRowColumn result = new GridRowColumn() {
				Y = p.Y / 2 * 2,
				Width = 2,
				Height = 2
			};

			switch(CpuType) {
				case CpuType.Snes:
					result.X = p.X;
					result.DisplayValue = $"{result.X * 2}, {result.Y / 2}";
					break;

				case CpuType.Nes:
					result.X = p.X / 2 * 2;
					result.DisplayValue = $"{result.X / 2}, {result.Y / 2 - 1}";
					break;

				case CpuType.Gameboy:
					result.X = p.X / 2 * 2;
					result.DisplayValue = $"{result.X / 2}, {result.Y / 2}";
					break;

				case CpuType.Pce:
					result.X = p.X;
					result.DisplayValue = $"{result.X}, {result.Y / 2}";
					break;

				default:
					throw new Exception("Invalid cpu type");
			}

			GridHighlightPoint = result;
		}

		public void UpdateConfig()
		{
			if(ConsoleConfig is SnesEventViewerConfig snesCfg) {
				DebugApi.SetEventViewerConfig(CpuType, snesCfg.ToInterop());
			} else if(ConsoleConfig is NesEventViewerConfig nesCfg) {
				DebugApi.SetEventViewerConfig(CpuType, nesCfg.ToInterop());
			} else if(ConsoleConfig is GbEventViewerConfig gbCfg) {
				DebugApi.SetEventViewerConfig(CpuType, gbCfg.ToInterop());
			} else if(ConsoleConfig is PceEventViewerConfig pceCfg) {
				DebugApi.SetEventViewerConfig(CpuType, pceCfg.ToInterop());
			}
		}

		public void EnableAllEventTypes()
		{
			foreach(PropertyInfo prop in ConsoleConfig.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
				if(prop.PropertyType == typeof(EventViewerCategoryCfg)) {
					((EventViewerCategoryCfg)prop.GetValue(ConsoleConfig)!).Visible = true;
				}
			}
		}

		public void DisableAllEventTypes()
		{
			foreach(PropertyInfo prop in ConsoleConfig.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
				if(prop.PropertyType == typeof(EventViewerCategoryCfg)) {
					((EventViewerCategoryCfg)prop.GetValue(ConsoleConfig)!).Visible = false;
				}
			}
		}

		public enum EventViewerTab
		{
			PpuView,
			ListView,
		}

		public static string GetEventDetails(DebugEventInfo evt, bool singleLine)
		{
			bool isDma = evt.DmaChannel >= 0 && (evt.Operation.Type == MemoryOperationType.DmaWrite || evt.Operation.Type == MemoryOperationType.DmaRead);

			List<string> details = new List<string>();
			if(evt.Flags.HasFlag(EventFlags.PreviousFrame)) {
				details.Add("Previous frame");
			}
			if(evt.Flags.HasFlag(EventFlags.NesPpuSecondWrite)) {
				details.Add("Second register write");
			} else if(evt.Flags.HasFlag(EventFlags.NesPpuFirstWrite)) {
				details.Add("First register write");
			}
			if(evt.Flags.HasFlag(EventFlags.HasTargetMemory)) {
				details.Add("Target: " + evt.TargetMemory.MemType.GetShortName() + " $" + evt.TargetMemory.Address.ToString(evt.TargetMemory.MemType.GetFormatString()));
			}

			if(evt.Type == DebugEventType.Breakpoint && evt.BreakpointId >= 0) {
				var breakpoints = BreakpointManager.Breakpoints;
				if(evt.BreakpointId < breakpoints.Count) {
					Breakpoint bp = breakpoints[evt.BreakpointId];
					string bpInfo = "Breakpoint - ";
					bpInfo += "CPU: " + ResourceHelper.GetEnumText(bp.CpuType);
					bpInfo += singleLine ? " - " : Environment.NewLine;
					bpInfo += "Type: " + bp.ToReadableType();
					bpInfo += singleLine ? " - " : Environment.NewLine;
					bpInfo += "Addresses: " + bp.GetAddressString(true);
					bpInfo += singleLine ? " - " : Environment.NewLine;
					if(bp.Condition.Length > 0) {
						bpInfo += singleLine ? " - " : Environment.NewLine;
						bpInfo += "Condition: " + bp.Condition;
					}
					details.Add(bpInfo);
				}
			}

			if(isDma) {
				string dmaInfo = "";
				bool indirectHdma = false;
				if((evt.DmaChannel & EventViewerViewModel.HdmaChannelFlag) != 0) {
					indirectHdma = evt.DmaChannelInfo.HdmaIndirectAddressing;
					dmaInfo += "HDMA #" + (evt.DmaChannel & 0x07);
					dmaInfo += indirectHdma ? " (indirect)" : "";
					dmaInfo += singleLine ? " - " : Environment.NewLine;
					dmaInfo += "Line Counter: $" + evt.DmaChannelInfo.HdmaLineCounterAndRepeat.ToString("X2");
				} else {
					dmaInfo += "DMA #" + (evt.DmaChannel & 0x07);
				}

				dmaInfo += singleLine ? " - " : Environment.NewLine;
				dmaInfo += "Mode: " + evt.DmaChannelInfo.TransferMode;
				dmaInfo += singleLine ? " - " : Environment.NewLine;

				int aBusAddress;
				if(indirectHdma) {
					aBusAddress = (evt.DmaChannelInfo.SrcBank << 16) | evt.DmaChannelInfo.TransferSize;
				} else {
					aBusAddress = (evt.DmaChannelInfo.SrcBank << 16) | evt.DmaChannelInfo.SrcAddress;
				}

				if(!evt.DmaChannelInfo.InvertDirection) {
					dmaInfo += "$" + aBusAddress.ToString("X4") + " -> $" + (0x2100 | evt.DmaChannelInfo.DestAddress).ToString("X4");
				} else {
					dmaInfo += "$" + aBusAddress.ToString("X4") + " <- $" + (0x2100 | evt.DmaChannelInfo.DestAddress).ToString("X4");
				}

				details.Add(dmaInfo.Trim());
			}

			if(singleLine) {
				if(details.Count > 0) {
					return "[" + string.Join("] [", details) + "]";
				} else {
					return "";
				}
			} else {
				return string.Join(Environment.NewLine, details);
			}
		}
	}

	public class DebugEventViewModel : INotifyPropertyChanged
	{
		private DebugEventInfo[] _events = Array.Empty<DebugEventInfo>();
		private int _index;

		public event PropertyChangedEventHandler? PropertyChanged;

		private string _programCounter = "";
		public string ProgramCounter
		{
			get
			{
				UpdateFields();
				return _programCounter;
			}
		}

		public string Scanline { get; set; } = "";
		public string Cycle { get; set; } = "";
		public string Type { get; set; } = "";
		public string Address { get; set; } = "";
		public string Value { get; set; } = "";
		public string Details { get; set; } = "";

		public DebugEventViewModel(DebugEventInfo[] events, int index)
		{
			Update(events, index);
		}

		public void Update(DebugEventInfo[] events, int index)
		{
			_events = events;
			_index = index;
			
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ProgramCounter"));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Scanline"));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Cycle"));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Type"));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Address"));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Details"));
		}

		private void UpdateFields()
		{
			DebugEventInfo evt = _events[_index];
			_programCounter = "$" + evt.ProgramCounter.ToString("X4");
			Scanline = evt.Scanline.ToString();
			Cycle = evt.Cycle.ToString();
			string address = "";
			if(evt.Type == DebugEventType.Register) {
				address += "$" + evt.Operation.Address.ToString("X4");
				if(evt.RegisterId >= 0) {
					address += $" ({evt.GetRegisterName()} - ${evt.RegisterId:X2})";
				}
			}
			Address = address;
			Value = evt.Type == DebugEventType.Register ? "$" + evt.Operation.Value.ToString("X2") : "";
			Type = ResourceHelper.GetEnumText(evt.Type);
			if(evt.Type == DebugEventType.Register) {
				Type += evt.Operation.Type.IsWrite() ? " (W)" : " (R)";
			}
			Details = EventViewerViewModel.GetEventDetails(evt, true);
		}
	}
}
