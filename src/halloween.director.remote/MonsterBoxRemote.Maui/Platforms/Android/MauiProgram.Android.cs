using Microsoft.Maui.Handlers;

namespace MonsterBoxRemote.Maui;

public static partial class MauiProgram
{
	static partial void RegisterPlatformHandlers(MauiAppBuilder builder)
	{
		builder.ConfigureMauiHandlers(handlers =>
		{
			// Strips the default Android Picker chrome (background/margins/padding)
			// so it matches the app's flat, custom-colored button styling. iOS
			// intentionally does not get an equivalent handler - see agents.md /
			// the UI design review notes on respecting the native wheel picker.
			PickerHandler.Mapper.AppendToMapping("StylelessPicker", (handler, view) =>
			{
				var control = handler.PlatformView;
				control.Background = null;

				// LayoutParameters is null before the platform view has been attached
				// to a parent (e.g. the first layout pass) - MarginLayoutParams's copy
				// constructor NPEs reading .width off a null source in that case, so
				// fall back to sensible defaults instead of copying.
				var source = control.LayoutParameters;
				var layoutParams = source is null
					? new Android.Views.ViewGroup.MarginLayoutParams(
						Android.Views.ViewGroup.LayoutParams.MatchParent,
						Android.Views.ViewGroup.LayoutParams.WrapContent)
					: new Android.Views.ViewGroup.MarginLayoutParams(source);
				layoutParams.SetMargins(0, 0, 0, 0);
				control.LayoutParameters = layoutParams;
				control.SetPadding(0, 0, 0, 0);
			});
		});
	}
}
