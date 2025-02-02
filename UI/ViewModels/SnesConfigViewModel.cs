﻿using Avalonia.Controls;
using Mesen.Config;
using Mesen.Utilities;
using ReactiveUI.Fody.Helpers;
using System;

namespace Mesen.ViewModels
{
	public class SnesConfigViewModel : DisposableViewModel
	{
		[Reactive] public SnesConfig Config { get; set; }
		[Reactive] public SnesConfig OriginalConfig { get; set; }
		[Reactive] public SnesConfigTab SelectedTab { get; set; } = 0;

		public SnesInputConfigViewModel Input { get; private set; }

		public Enum[] AvailableRegions => new Enum[] {
			ConsoleRegion.Auto,
			ConsoleRegion.Ntsc,
			ConsoleRegion.Pal			
		};

		public SnesConfigViewModel()
		{
			Config = ConfigManager.Config.Snes;
			OriginalConfig = Config.Clone();
			Input = new SnesInputConfigViewModel(Config);

			if(Design.IsDesignMode) {
				return;
			}

			AddDisposable(Input);
			AddDisposable(ReactiveHelper.RegisterRecursiveObserver(Config, (s, e) => { Config.ApplyConfig(); }));
		}
   }

	public enum SnesConfigTab
	{
		General,
		Audio,
		Emulation,
		Input,
		Overclocking,
		Video,
		Bsx
	}
}
