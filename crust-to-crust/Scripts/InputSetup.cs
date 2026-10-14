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
    public const string ToggleLantern = "toggle_lantern", Pause = "pause_run", Back = "back_menu";
    public const string AimLeft = "aim_left", AimRight = "aim_right", AimUp = "aim_up", AimDown = "aim_down";
    public static float ReadMovement()
    {
        int keyboard = (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0)
            - (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0);
        if (keyboard != 0) return keyboard;
        foreach (int device in Input.GetConnectedJoypads())
        {
            int dpad = (Input.IsJoyButtonPressed(device,JoyButton.DpadRight) ? 1 : 0)
                - (Input.IsJoyButtonPressed(device,JoyButton.DpadLeft) ? 1 : 0);
            if (dpad != 0) return dpad;
            float axis = Input.GetJoyAxis(device,JoyAxis.LeftX);
            const float deadzone = 0.35f;
            if (Mathf.Abs(axis) > deadzone) return Mathf.Sign(axis)*(Mathf.Abs(axis)-deadzone)/(1f-deadzone);
        }
        return 0f;
    }

	public static void Ensure()
	{
		Bind(MoveLeft, Key.A, Key.Left, JoyButton.DpadLeft, -1f);
		Bind(MoveRight, Key.D, Key.Right, JoyButton.DpadRight, 1f);
        Action(ToggleLantern,Key.F,JoyButton.Y);
        Add(ToggleLantern,new InputEventMouseButton { ButtonIndex=MouseButton.Left });
        Action(Pause,Key.Escape,JoyButton.Start); Action(Back,Key.Escape,JoyButton.B);
        Aim(AimLeft,JoyAxis.RightX,-1); Aim(AimRight,JoyAxis.RightX,1);
        Aim(AimUp,JoyAxis.RightY,-1); Aim(AimDown,JoyAxis.RightY,1);
	}

	private static void Bind(string action, Key primary, Key secondary, JoyButton dpad, float stickDirection)
	{
		if (!InputMap.HasAction(action))
			InputMap.AddAction(action, 0.3f);
		Add(action, new InputEventKey { PhysicalKeycode = primary });
		Add(action, new InputEventKey { PhysicalKeycode = secondary });
		Add(action, new InputEventJoypadButton { ButtonIndex = dpad });
		Add(action, new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = stickDirection });
	}
    private static void Add(string action,InputEvent binding)
    {
        foreach (InputEvent existing in InputMap.ActionGetEvents(action))
            if (existing.IsMatch(binding,true)) return;
        InputMap.ActionAddEvent(action,binding);
    }
    private static void Action(string name,Key key,JoyButton button)
    {
        if (!InputMap.HasAction(name)) InputMap.AddAction(name);
        Add(name,new InputEventKey { Keycode = key });
        Add(name,new InputEventKey { PhysicalKeycode = key });
        Add(name,new InputEventJoypadButton { ButtonIndex = button });
    }
    private static void Aim(string name,JoyAxis axis,float value)
    {
        if (!InputMap.HasAction(name)) InputMap.AddAction(name,0.2f);
        Add(name,new InputEventJoypadMotion { Axis = axis,AxisValue = value });
    }
}
