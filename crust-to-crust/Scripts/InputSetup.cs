using Godot;

/// <summary>
/// Реєструє дії вводу (Input Map) у коді, щоб гра працювала без ручного налаштування проєкту.
/// Використовуються фізичні клавіші, тому A/D працюють і з українською розкладкою.
/// Також підтримується стрілки, D-pad та лівий стік геймпада.
/// </summary>
public static class InputSetup
{
	public const string MoveLeft = "move_left";
	public const string MoveRight = "move_right";

	public static void Ensure()
	{
		Bind(MoveLeft, Key.A, Key.Left, JoyButton.DpadLeft, -1f);
		Bind(MoveRight, Key.D, Key.Right, JoyButton.DpadRight, 1f);
	}

	private static void Bind(string action, Key primary, Key secondary, JoyButton dpad, float stickDirection)
	{
		if (!InputMap.HasAction(action))
			InputMap.AddAction(action, 0.3f);
		if (InputMap.ActionGetEvents(action).Count > 0)
			return; // дію вже налаштовано (у редакторі або раніше)

		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = primary });
		InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = secondary });
		InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = dpad });
		InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = stickDirection });
	}
}
