using Godot;

public partial class Hud : CanvasLayer
{
    private Label _scoreLabel, _depthLabel, _heatLabel, _lapLabel, _shieldLabel, _hintLabel;
    private Label _gameOverStats, _walletLabel, _speedLabel, _pauseStats;
    private Control _gameOverPanel, _startMenuPanel, _pauseOverlay, _shop;
    private Button _pauseButton, _resumeButton, _shopBack;
    private readonly Button[] _shopItems = new Button[UpgradeCatalog.Count], _skinItems = new Button[CharacterCatalog.Count];
    private readonly Button[] _trailItems = new Button[TrailStyle.Count];
    private ProgressBar _heatBar;
    private Control _lanternStatus;
    private ProgressBar _chargeBar;
    private Label _chargeText;
    private StyleBoxFlat _heatFill;
    private ShaderMaterial _vignette;
    private TextureRect _menuHero;
    private LineEdit _seedInput;
    private Control _settings;
    private Control _settingsReturn;
    private Control _shopReturn;
    private Button _settingsBack, _buySkin, _unlockForm;
    private TextureRect _shopHero;
    private readonly Label[] _skinLabels=new Label[CharacterCatalog.Count];
    private readonly Label[] _skinPrices=new Label[CharacterCatalog.Count];
    private Label _heroStatus, _heroForm, _collectionLabel;
    private static readonly Color WorkshopCyan=new Color("71d8cd"), WorkshopCopper=new Color("c69560");
    private OptionButton _formFilter;
    private TrailPreview _trailPreview;
    private PanelContainer _shopCard;
    private GridContainer _skinGrid,_equipmentGrid;
    private ScrollContainer _skinScroll;
    private ScrollContainer _trailScroll;
    private Label _heroName;
    private int _previewSkin;
    private Label _seedError;
    private ProgressBar _journeyBar;
    private Label _destinationLabel, _tutorialText;
    private Control _tutorialPanel;
    private int _tutorialStep;
    private bool _tutorialActive;
    private Label _zoneLabel, _coreBanner;
    private float _announcementTime;
    public bool TutorialActive => _tutorialActive;
    public bool TutorialWaterPending => _tutorialActive && _tutorialStep == 2;
    private bool _started, _finished;
    private int _coins, _lap = 1;
    private float _totalDistance, _heatLevel, _pulseTime, _vignetteLevel;
    private bool _burning;
    private Label _bonusLabel;
    private float _doubleBonus, _magnetBonus, _uiTicker, _shownDepth;
    private static bool _restartImmediately;
    private static readonly Color Ink = new Color("141719"), Edge = new Color("44463e"), Aqua = new Color("d8b477");
    [Signal] public delegate void StartRequestedEventHandler();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SaveData.Load();
        var root = GetNode<Control>("Control");
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.Theme = BuildTheme();
        _scoreLabel = GetNode<Label>("Control/ScoreLabel");
        _depthLabel = GetNode<Label>("Control/DepthLabel");
        _heatLabel = GetNode<Label>("Control/HeatLabel");
        _gameOverPanel = GetNode<Control>("Control/GameOverPanel");
        _startMenuPanel = GetNode<Control>("Control/StartMenuPanel");
        _gameOverPanel.Hide();
        BuildVignette(root);
        BuildStatus(root);
        BuildMainMenu();
        BuildGameOver();
        BuildPauseMenu(root);
        BuildShop(root);
        BuildSettings(root);
        GetNode<Button>("Control/StartMenuPanel/StartButton").GrabFocus();
        if (_restartImmediately)
        {
            _restartImmediately = false;
            Callable.From(StartRun).CallDeferred();
        }
    }
    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFontSize = 18 };
        theme.SetColor("font_color","Label",new Color("d5e2e6"));
        theme.SetColor("font_color","Button",new Color("e3dfd4"));
        theme.SetColor("font_hover_color","Button",new Color("fff3dc"));
        theme.SetColor("font_disabled_color","Button",new Color("757e7c"));
        theme.SetStylebox("normal","Button",Card(new Color("202727"),new Color("454f4d"),3));
        theme.SetStylebox("hover","Button",Card(new Color("2a3533"),new Color("79a69c"),3));
        var pressed=Card(new Color("151e1e"),new Color("9cc9bb"),3); pressed.ContentMarginTop=14; pressed.ContentMarginBottom=10;
        theme.SetStylebox("pressed","Button",pressed);
        theme.SetStylebox("disabled","Button",Card(new Color("171c1c"),new Color("303836"),3));
        var focus=Card(new Color(0,0,0,0),Aqua,3); focus.ShadowSize=0;
        theme.SetStylebox("focus","Button",focus);
        theme.SetStylebox("panel","PanelContainer",Card(new Color("10191b"),new Color("485552"),4));
        theme.SetStylebox("panel","TabContainer",new StyleBoxEmpty());
        foreach(string state in new[] {"tab_unselected","tab_selected","tab_hovered"})
        {
            var tab=Card(new Color(state=="tab_selected" ? "202c2c" : "111b1d"),new Color("38514c"),2);
            tab.SetBorderWidthAll(0); tab.BorderWidthBottom=state=="tab_selected" ? 2 : 1;
            tab.BorderColor=state=="tab_selected" ? Aqua : new Color("38514c"); tab.ShadowSize=0;
            tab.ContentMarginTop=9; tab.ContentMarginBottom=9;
            theme.SetStylebox(state,"TabBar",tab);
            theme.SetStylebox(state,"TabContainer",tab);
        }
        theme.SetColor("font_selected_color","TabBar",new Color("f0ddbb"));
        theme.SetColor("font_unselected_color","TabBar",new Color("9caaa4"));
        theme.SetColor("font_selected_color","TabContainer",new Color("f0ddbb"));
        theme.SetColor("font_unselected_color","TabContainer",new Color("9caaa4"));
        theme.SetStylebox("normal","OptionButton",Card(new Color("182425"),new Color("526861"),3));
        theme.SetStylebox("hover","OptionButton",Card(new Color("233332"),new Color("80aca1"),3));
        theme.SetStylebox("focus","OptionButton",focus);
        theme.SetStylebox("normal","LineEdit",Card(new Color("0c1518"),new Color("435c57"),2));
        theme.SetStylebox("focus","LineEdit",focus);
        var rail=Card(new Color("0a1315"),new Color("354743"),2); rail.ContentMarginTop=3; rail.ContentMarginBottom=3; rail.ShadowSize=0;
        var fill=Card(new Color("679b8e"),new Color("679b8e"),2); fill.ContentMarginTop=3; fill.ContentMarginBottom=3; fill.ShadowSize=0;
        theme.SetStylebox("slider","HSlider",rail); theme.SetStylebox("grabber_area","HSlider",fill); theme.SetStylebox("grabber_area_highlight","HSlider",fill);
        return theme;
    }
    private static StyleBoxFlat Card(Color fill, Color edge, int radius)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = edge,
            ShadowColor = new Color(0,0,0,0.35f), ShadowSize = 7,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12 };
        style.SetCornerRadiusAll(radius); style.SetBorderWidthAll(1);
        return style;
    }
    private static void Clear(Control parent)
    {
        foreach (Node child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
        parent.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        parent.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (parent is Panel panel) panel.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
    }
    private static Label Text(Control parent, string text, int size = 18, Color? color = null)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size",size);
        if (color.HasValue) label.AddThemeColorOverride("font_color",color.Value);
        parent.AddChild(label); return label;
    }
    private static Button Button(Control parent, string text, System.Action action, string name = "")
    {
        var button = new CaveButton { Text = text, CustomMinimumSize = new Vector2(0,48) };
        if (name != "") button.Name = name;
        parent.AddChild(button); button.Pressed += action; return button;
    }
    private static void Primary(Button button)
    {
        if(button is CaveButton cave) cave.Primary=true;
        button.AddThemeStyleboxOverride("normal",Card(new Color("9f8558"),new Color("c1a579"),3));
        button.AddThemeStyleboxOverride("hover",Card(new Color("b49a69"),new Color("e3c797"),3));
        button.AddThemeStyleboxOverride("pressed",Card(new Color("796444"),new Color("ddbc88"),3));
        button.AddThemeColorOverride("font_color",new Color("111919"));
        button.AddThemeColorOverride("font_hover_color",new Color("111919"));
        button.AddThemeColorOverride("font_pressed_color",new Color("111919"));
        button.AddThemeColorOverride("font_focus_color",new Color("111919"));
    }
    private static VBoxContainer Section(VBoxContainer parent,string title)
    {
        var group=new VBoxContainer(); parent.AddChild(group); group.AddThemeConstantOverride("separation",8);
        var heading=Text(group,title,12,new Color("8aaba0"));
        var line=new HSeparator(); group.AddChild(line);
        return group;
    }
    private static TextureRect Art(Control parent, Texture2D texture, Vector2 size)
    {
        var art = new TextureRect { Texture = texture, CustomMinimumSize = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(art); return art;
    }
    private static void FullShade(Control root, float alpha)
    {
        var shade = new ColorRect { Color = new Color(0.015f,0.025f,0.04f,alpha), MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
    private static VBoxContainer CenterCard(Control root, float width)
    {
        var scroll = new ScrollContainer { FollowFocus = true, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll); scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        scroll.OffsetTop = 16; scroll.OffsetBottom = -16;
        var center = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width,0) }; center.AddChild(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation",12); panel.AddChild(box);
        return box;
    }
    private void BuildMainMenu()
    {
        Clear(_startMenuPanel);
        var backdrop=Art(_startMenuPanel,GD.Load<Texture2D>("res://Textures/ArtRefresh/crystal_chasm.png"),Vector2.Zero);
        backdrop.StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered;
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.Modulate=new Color(0.46f,0.55f,0.58f);
        FullShade(_startMenuPanel,0.42f);
        var title=Text(_startMenuPanel,"CRUST\nTO CRUST",82,new Color("eee5d2"));
        title.AddThemeFontOverride("font",GD.Load<Font>("res://Fonts/BebasNeue-Regular.ttf"));
        title.AnchorLeft=0.09f; title.AnchorTop=0.12f;
        var start=Button(_startMenuPanel,"ГРАТИ",StartRun,"StartButton");
        var characters=Button(_startMenuPanel,"МАЙСТЕРНЯ",OpenShop);
        var settings=Button(_startMenuPanel,"НАЛАШТУВАННЯ",()=>OpenSettings(_startMenuPanel));
        int index=0;
        foreach(var button in new[] { start,characters,settings })
        {
            button.AnchorLeft=0.09f; button.AnchorRight=0.40f;
            button.AnchorTop=0.48f; button.AnchorBottom=0.48f;
            button.OffsetTop=index*59; button.OffsetBottom=index*59+48;
            button.Alignment=HorizontalAlignment.Left;
            index++;
        }
        Primary(start); start.AddThemeFontSizeOverride("font_size",22);
        var halo=Art(_startMenuPanel,GameAssets.Halo,Vector2.Zero);
        halo.AnchorLeft=0.48f; halo.AnchorRight=0.93f; halo.AnchorTop=0.16f; halo.AnchorBottom=0.85f;
        halo.Modulate=new Color(0.28f,0.45f,0.40f,0.28f);
        _menuHero=Art(_startMenuPanel,CharacterCatalog.Texture(SaveData.SelectedSkin),Vector2.Zero);
        _menuHero.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
        _menuHero.AnchorLeft=0.58f; _menuHero.AnchorRight=0.84f; _menuHero.AnchorTop=0.29f; _menuHero.AnchorBottom=0.69f;
        var best=Text(_startMenuPanel,$"{Mathf.FloorToInt(SaveData.BestDistance):N0} м  ·  рекорд",16,new Color("b3b9ab"));
        best.AnchorLeft=0.09f; best.AnchorTop=0.88f;
        var wallet=Text(_startMenuPanel,$"{SaveData.Wallet}  монет",16,Aqua);
        wallet.AnchorLeft=0.75f; wallet.AnchorTop=0.88f;
    }

    public void StartRun()
    {
        if (_started) return;
        var level = GetParent() as LevelGenerator;
        if (!string.IsNullOrWhiteSpace(_seedInput?.Text))
        {
            if (!int.TryParse(_seedInput.Text.Trim(),out int seed)) { _seedError.Text = "Потрібне ціле число."; OpenSettings(_startMenuPanel); _seedInput.GetParent<Control>().Show(); _seedInput.GrabFocus(); return; }
            level?.SetRunSeed(seed);
        }
        else if (level != null) level.SetRunSeed(level.RunSeed);
        _started = true; _startMenuPanel.Hide(); _pauseButton.Show();
        _scoreLabel.Show(); _depthLabel.Show(); _lapLabel.Show(); _speedLabel.Show();
        _hintLabel.Show();
        var tween = CreateTween(); tween.TweenInterval(7f);
        tween.TweenProperty(_hintLabel,"modulate:a",0f,1f);
        EmitSignal(SignalName.StartRequested);
        _journeyBar.Show(); _destinationLabel.Show(); _zoneLabel.Show();
        if (!SaveData.TutorialComplete || _tutorialActive) BeginTutorial();
    }
    private void BuildGameOver()
    {
        Clear(_gameOverPanel); FullShade(_gameOverPanel,0.88f);
        PlaceEnd(Text(_gameOverPanel,"ЗАБІГ ЗАВЕРШЕНО",30,new Color("eee5d2")),-250,-196);
        _gameOverStats=Text(_gameOverPanel,"",19); PlaceEnd(_gameOverStats,-174,12);
        PlaceEnd(Button(_gameOverPanel,"ЩЕ РАЗ",Restart,"RestartButton"),38,90);
        PlaceEnd(Button(_gameOverPanel,"МАЙСТЕРНЯ",OpenShop),104,152);
        PlaceEnd(Button(_gameOverPanel,"МЕНЮ",()=>Reload(false)),166,214);
        var seed=Button(_gameOverPanel,"Повторити маршрут",()=>{ LevelGenerator.RequestedSeed=(GetParent() as LevelGenerator)?.RunSeed; Reload(true); },"ReplaySeedButton");
        seed.AddThemeFontSizeOverride("font_size",14); PlaceEnd(seed,230,268);
    }

    private static void PlaceEnd(Control control, float top, float bottom)
    {
        control.AnchorLeft = 0.5f; control.AnchorRight = 0.5f; control.AnchorTop = 0.5f; control.AnchorBottom = 0.5f;
        control.OffsetLeft = -218; control.OffsetRight = 218; control.OffsetTop = top; control.OffsetBottom = bottom;
        if (control is Label label) label.HorizontalAlignment = HorizontalAlignment.Center;
    }
    private void BuildStatus(Control root)
    {
        foreach (Label label in new[] {_scoreLabel,_depthLabel,_heatLabel})
        {
            label.RemoveThemeFontOverride("font"); label.AddThemeFontSizeOverride("font_size",20);
        }
        _scoreLabel.Position = new Vector2(24,20); _depthLabel.Position = new Vector2(24,50);
        _scoreLabel.Text = "МОНЕТИ: 0";
        _scoreLabel.Hide(); _depthLabel.Hide();
        _lapLabel = Text(root,"КОЛО 1",15,Aqua); _lapLabel.Position = new Vector2(24,80); _lapLabel.Hide();
        _speedLabel = Text(root,"",16,new Color("a9bbc3")); _speedLabel.Position = new Vector2(24,105); _speedLabel.Hide();
        _shieldLabel = Text(root,"ЩИТ ГОТОВИЙ",17,Aqua); _shieldLabel.Position = new Vector2(24,134); _shieldLabel.Hide();
        _bonusLabel = Text(root,"",16,new Color("ffce79")); _bonusLabel.Position = new Vector2(24,162);
        _lanternStatus=new Control { Name="LanternCharge",Visible=false,AnchorLeft=1,AnchorRight=1,OffsetLeft=-196,OffsetRight=-24,OffsetTop=88,OffsetBottom=142,MouseFilter=Control.MouseFilterEnum.Ignore };
        root.AddChild(_lanternStatus);
        var cell=Art(_lanternStatus,GameAssets.Battery,Vector2.Zero); cell.Position=new Vector2(0,0); cell.Size=new Vector2(25,43);
        _chargeText=Text(_lanternStatus,"",14,new Color("93c8cb")); _chargeText.Position=new Vector2(34,0);
        _chargeBar=new ProgressBar { MinValue=0,MaxValue=18,Step=0.01,ShowPercentage=false,Position=new Vector2(34,28),Size=new Vector2(138,7),MouseFilter=Control.MouseFilterEnum.Ignore };
        var chargeBackground=new StyleBoxFlat { BgColor=Ink,BorderColor=Edge };
        chargeBackground.SetBorderWidthAll(1); chargeBackground.SetCornerRadiusAll(2);
        var chargeFill=new StyleBoxFlat { BgColor=new Color("70adb0") }; chargeFill.SetCornerRadiusAll(2);
        _chargeBar.AddThemeStyleboxOverride("background",chargeBackground);
        _chargeBar.AddThemeStyleboxOverride("fill",chargeFill); _lanternStatus.AddChild(_chargeBar);
        _journeyBar = new ProgressBar { Name = "JourneyProgress", MinValue = 0, MaxValue = 1, Step = 0.0001,
            ShowPercentage = false, Position = new Vector2(24,198), Size = new Vector2(220,10), Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        _journeyBar.AddThemeStyleboxOverride("background",Card(Ink,Edge,3));
        _journeyBar.AddThemeStyleboxOverride("fill",Card(Aqua,Aqua,3)); root.AddChild(_journeyBar);
        var coreMarker = new ColorRect { Color = new Color("ffb556"),Position = new Vector2(109,0),Size = new Vector2(2,10),MouseFilter = Control.MouseFilterEnum.Ignore };
        _journeyBar.AddChild(coreMarker);
        _destinationLabel = Text(root,"",15,Aqua); _destinationLabel.Position = new Vector2(24,214); _destinationLabel.Hide();
        _zoneLabel = Text(root,"",14,new Color("a9bbc3")); _zoneLabel.Position = new Vector2(24,246); _zoneLabel.Hide();
        _coreBanner = Text(root,"",17,new Color("ffb556")); _coreBanner.Position = new Vector2(24,282); _coreBanner.Hide();
        _tutorialPanel = new VBoxContainer { Position = new Vector2(24,360), CustomMinimumSize = new Vector2(260,0),Visible = false };
        root.AddChild(_tutorialPanel); Text(_tutorialPanel,"НАВЧАННЯ",13,Aqua);
        _tutorialText = Text(_tutorialPanel,"",16); _tutorialText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Button(_tutorialPanel,"ПРОПУСТИТИ",FinishTutorial);
        _hintLabel = Text(root,"A / D · рух     Мишка · напрям світла     ЛКМ · ліхтар     Esc · пауза",16);
        _hintLabel.AnchorRight = 1; _hintLabel.AnchorTop = 1; _hintLabel.AnchorBottom = 1;
        _hintLabel.OffsetTop = -48; _hintLabel.OffsetBottom = -18;
        _hintLabel.HorizontalAlignment = HorizontalAlignment.Center; _hintLabel.Hide();
        _heatLabel.OffsetLeft = -210; _heatLabel.OffsetRight = 210;
        _heatLabel.AddThemeFontSizeOverride("font_size",18);
        _heatBar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -175, OffsetRight = 175,
            OffsetTop = 57, OffsetBottom = 68, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _heatFill = Card(new Color("f2ac5b"),new Color("f2ac5b"),5);
        _heatBar.AddThemeStyleboxOverride("fill",_heatFill);
        _heatBar.AddThemeStyleboxOverride("background",Card(Ink,Edge,5)); root.AddChild(_heatBar);
    }
    private void BuildPauseMenu(Control root)
    {
        _pauseButton=Button(root,"Ⅱ",TogglePause); _pauseButton.Hide();
        _pauseButton.AnchorLeft=1; _pauseButton.AnchorRight=1;
        _pauseButton.OffsetLeft=-76; _pauseButton.OffsetRight=-24; _pauseButton.OffsetTop=20; _pauseButton.OffsetBottom=68;
        _pauseOverlay=new Control { Name="PauseOverlay",Visible=false }; root.AddChild(_pauseOverlay);
        _pauseOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); FullShade(_pauseOverlay,0.82f);
        var box=CenterCard(_pauseOverlay,380);
        Text(box,"ПАУЗА",32,new Color("eee5d2")).HorizontalAlignment=HorizontalAlignment.Center;
        _pauseStats=Text(box,"",16); _pauseStats.HorizontalAlignment=HorizontalAlignment.Center;
        _resumeButton=Button(box,"ПРОДОВЖИТИ",TogglePause);
        Button(box,"НАЛАШТУВАННЯ",()=>OpenSettings(_pauseOverlay));
        Button(box,"МЕНЮ",()=>Reload(false));
    }
    private void BuildSettings(Control root)
    {
        _settings=new Control { Name="Settings",Visible=false }; root.AddChild(_settings);
        _settings.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); FullShade(_settings,0.96f);
        var box=CenterCard(_settings,480);
        Text(box,"НАЛАШТУВАННЯ",28,new Color("eee5d2"));
        var audio=Section(box,"ЗВУК");
        Volume(audio,"Музика",SaveData.MusicVolume,v=>SaveData.MusicVolume=v);
        Volume(audio,"Ефекти",SaveData.SfxVolume,v=>SaveData.SfxVolume=v);
        var visuals=Section(box,"ЗОБРАЖЕННЯ");
        Volume(visuals,"Тряска",SaveData.CameraShake,v=>SaveData.CameraShake=v);
        Volume(visuals,"Спалахи",SaveData.FlashIntensity,v=>SaveData.FlashIntensity=v);
        var light=new CheckButton { Text="Економне світло",ButtonPressed=SaveData.EconomyLighting };
        visuals.AddChild(light);
        light.Toggled+=low=>{ SaveData.EconomyLighting=low; if(GetParent() is LevelGenerator level) level.CrystalLightBudget=low ? 3 : 6; };
        var advanced=new VBoxContainer { Visible=false }; box.AddChild(advanced);
        var more=Button(box,"КЕРУВАННЯ І МАРШРУТ",()=>advanced.Visible=!advanced.Visible);
        box.MoveChild(more,box.GetChildCount()-2);
        Text(advanced,"A / D · рух    Мишка · напрям світла\nЛКМ · ліхтар    Esc · пауза",15);
        Text(advanced,"Геймпад: лівий стік · рух, правий · світло\nY · ліхтар, Start · пауза, B · назад",14);
        _seedInput=new LineEdit { Name="SeedInput",PlaceholderText="Seed маршруту",MaxLength=11 };
        advanced.AddChild(_seedInput);
        _seedError=Text(advanced,"",14,new Color("ffb556"));
        Button(advanced,"Навчання",()=>{ CloseSettings(); BeginTutorial(); StartRun(); });
        _settingsBack=Button(box,"НАЗАД",CloseSettings);
    }
    private void OpenSettings(Control from)
    {
        _settingsReturn=from; from.Hide(); _settings.Show();
        _seedInput.Editable=!_started;
        _settingsBack.GrabFocus();
    }
    private void CloseSettings()
    {
        SaveData.Save(); _settings.Hide(); _settingsReturn?.Show();
        if(_started) _resumeButton.GrabFocus();
        else GetNode<Button>("Control/StartMenuPanel/StartButton").GrabFocus();
    }

    private static void Volume(VBoxContainer box, string caption, float value, System.Action<float> apply)
    {
        var row = new HBoxContainer(); box.AddChild(row);
        var label = Text(row,caption,17); label.CustomMinimumSize = new Vector2(100,0);
        var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = value,
            CustomMinimumSize = new Vector2(0,30), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(slider);
        var number=Text(row,$"{value*100:0}%",15,new Color("bbc9bf")); number.CustomMinimumSize=new Vector2(48,0); number.HorizontalAlignment=HorizontalAlignment.Right;
        slider.ValueChanged += v => { number.Text=$"{v*100:0}%"; apply((float)v); AudioManager.ApplyVolumes(); SaveData.Save(); };
    }
    private void BuildShop(Control root)
    {
        _shop=new Control { Name="CharacterShop",Visible=false }; root.AddChild(_shop);
        _shop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _shop.Theme=BuildWorkshopTheme();
        var backdrop=new WorkshopBackdrop(); _shop.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var box=CenterCard(_shop,Mathf.Min(900,GetViewport().GetVisibleRect().Size.X-48));
        _shopCard=box.GetParent<PanelContainer>();
        _shopCard.AddThemeStyleboxOverride("panel",WorkshopPanel(new Color("142025"),WorkshopCopper,20));
        var header=new HBoxContainer(); box.AddChild(header);
        var heading=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; header.AddChild(heading);
        heading.AddThemeConstantOverride("separation",0);
        Text(heading,"МАЙСТЕРНЯ",28,new Color("f0e4cd"));
        _collectionLabel=Text(heading,"",12,new Color("92aaa7"));
        var wallet=new PanelContainer { SizeFlagsVertical=Control.SizeFlags.ShrinkCenter }; header.AddChild(wallet);
        wallet.AddThemeStyleboxOverride("panel",WorkshopPanel(new Color("0d181b"),new Color("866b46"),10));
        _walletLabel=Text(wallet,"",17,new Color("ecc67d"));
        var tabs=new TabContainer(); box.AddChild(tabs);
        var forms=new VBoxContainer { Name="Форми" }; tabs.AddChild(forms);
        _formFilter=new OptionButton { CustomMinimumSize=new Vector2(0,42) }; forms.AddChild(_formFilter);
        foreach(BodyForm form in System.Enum.GetValues<BodyForm>()) _formFilter.AddItem(CharacterCatalog.FormName(form));
        _formFilter.ItemSelected+=index=>{ _previewSkin=CharacterCatalog.BaseSkin((BodyForm)index); RefreshShop(); };
        var characters=new HBoxContainer(); forms.AddChild(characters);
        characters.AddThemeConstantOverride("separation",16);
        int columns=GetViewport().GetVisibleRect().Size.X>=1000 ? 3 : 2;
        var skinScroll=new ScrollContainer { Name="SkinScroll",CustomMinimumSize=new Vector2(columns*148+16,320),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true }; characters.AddChild(skinScroll);
        _skinScroll=skinScroll;
        var selection=new GridContainer { Columns=columns,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; skinScroll.AddChild(selection);
        _skinGrid=selection;
        selection.AddThemeConstantOverride("h_separation",8); selection.AddThemeConstantOverride("v_separation",8);
        for(int i=0;i<CharacterCatalog.Count;i++)
        {
            int id=i;
            var tile=Button(selection,"",()=>{ _previewSkin=id; RefreshShop(); }); _skinItems[i]=tile;
            tile.CustomMinimumSize=new Vector2(144,144); tile.TooltipText=CharacterCatalog.Get(i).Name;
            var icon=Art(tile,CharacterCatalog.Texture(i),Vector2.Zero); icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
            icon.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
            icon.OffsetLeft=26; icon.OffsetRight=-26; icon.OffsetTop=10; icon.OffsetBottom=86;
            _skinLabels[i]=Text(tile,CharacterCatalog.Get(i).Name,12); _skinLabels[i].HorizontalAlignment=HorizontalAlignment.Center;
            _skinLabels[i].AutowrapMode=TextServer.AutowrapMode.WordSmart;
            _skinLabels[i].SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide); _skinLabels[i].OffsetLeft=6; _skinLabels[i].OffsetRight=-6; _skinLabels[i].OffsetTop=-55; _skinLabels[i].OffsetBottom=-23;
            _skinPrices[i]=Text(tile,"",11); _skinPrices[i].HorizontalAlignment=HorizontalAlignment.Center;
            _skinPrices[i].SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
            _skinPrices[i].OffsetTop=-24; _skinPrices[i].OffsetBottom=-6;
        }
        var previewCard=new PanelContainer { Name="SkinPreviewCard",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; characters.AddChild(previewCard);
        previewCard.AddThemeStyleboxOverride("panel",WorkshopPanel(new Color("101c20"),new Color("47645e"),12));
        var preview=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; previewCard.AddChild(preview);
        preview.AddThemeConstantOverride("separation",8);
        _heroForm=Text(preview,"",11,WorkshopCopper); _heroForm.HorizontalAlignment=HorizontalAlignment.Center;
        var stand=new WorkshopBackdrop { DisplayStand=true,CustomMinimumSize=new Vector2(176,174) }; preview.AddChild(stand);
        _shopHero=Art(stand,CharacterCatalog.Texture(0),Vector2.Zero); _shopHero.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
        _shopHero.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _shopHero.OffsetLeft=40; _shopHero.OffsetRight=-40; _shopHero.OffsetTop=36; _shopHero.OffsetBottom=-36;
        _heroName=Text(preview,"",18,new Color("eee3cd")); _heroName.HorizontalAlignment=HorizontalAlignment.Center;
        _heroName.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        _heroStatus=Text(preview,"",12,WorkshopCyan); _heroStatus.HorizontalAlignment=HorizontalAlignment.Center;
        _heroStatus.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        _buySkin=Button(preview,"",()=>{
            if(SaveData.SelectOrBuySkin(_previewSkin)) GetParent().GetNode<Player>("Player").ApplySkin();
            RefreshShop();
        });
        Primary(_buySkin);
        _buySkin.AddThemeFontSizeOverride("font_size",16);
        _unlockForm=Button(preview,"",()=>{
            SaveData.SelectOrBuySkin(CharacterCatalog.BaseSkin(CharacterCatalog.Get(_previewSkin).Form));
            GetParent().GetNode<Player>("Player").ApplySkin(); RefreshShop();
        });
        _unlockForm.AddThemeFontSizeOverride("font_size",15);
        var equipmentScroll=new ScrollContainer { Name="Спорядження",CustomMinimumSize=new Vector2(0,360),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true }; tabs.AddChild(equipmentScroll);
        var equipment=new GridContainer { Columns=columns==3 ? 2 : 1,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; equipmentScroll.AddChild(equipment);
        _equipmentGrid=equipment;
        equipment.AddThemeConstantOverride("h_separation",12); equipment.AddThemeConstantOverride("v_separation",12);
        for(int i=0;i<UpgradeCatalog.Count;i++)
        {
            int item=i; _shopItems[i]=Button(equipment,"",()=>{ SaveData.Buy(item); RefreshShop(); });
            _shopItems[i].Alignment=HorizontalAlignment.Left;
            _shopItems[i].CustomMinimumSize=new Vector2(0,90); _shopItems[i].SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
            _shopItems[i].AddThemeFontSizeOverride("font_size",14);
        }
        var trails=new VBoxContainer { Name="Сліди" }; tabs.AddChild(trails);
        Text(trails,"ОБЕРИ СЛІД",15,Aqua);
        _trailPreview=new TrailPreview { CustomMinimumSize=new Vector2(0,88),MouseFilter=Control.MouseFilterEnum.Ignore }; trails.AddChild(_trailPreview);
        var trailScroll=new ScrollContainer { Name="TrailScroll",CustomMinimumSize=new Vector2(0,276),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true }; trails.AddChild(trailScroll);
        _trailScroll=trailScroll;
        tabs.TabChanged+=tab=>{ if(tab==2) Callable.From(()=>_trailScroll.EnsureControlVisible(_trailItems[SaveData.SelectedTrail])).CallDeferred(); };
        var trailList=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; trailScroll.AddChild(trailList); trailList.AddThemeConstantOverride("separation",7);
        for(int i=0;i<TrailStyle.Count;i++)
        {
            int id=i; _trailItems[i]=Button(trailList,"",()=>{ SaveData.SelectTrail(id); RefreshShop(); _trailPreview.StyleId=id; });
            _trailItems[i].AddThemeFontSizeOverride("font_size",15);
            _trailItems[i].Alignment=HorizontalAlignment.Left;
            _trailItems[i].AutowrapMode=TextServer.AutowrapMode.WordSmart;
            _trailItems[i].CustomMinimumSize=new Vector2(0,54);
        }
        _shopBack=Button(box,"НАЗАД ДО ГРИ",CloseShop);
        _shop.Resized+=ResizeShop;
    }
    private static StyleBoxFlat WorkshopPanel(Color fill,Color border,int padding=10)
    {
        var style=Card(fill,border,0); style.SetBorderWidthAll(2); style.ShadowSize=0;
        style.ContentMarginLeft=style.ContentMarginRight=padding;
        style.ContentMarginTop=style.ContentMarginBottom=padding;
        return style;
    }
    private static Theme BuildWorkshopTheme()
    {
        var theme=BuildTheme();
        theme.SetStylebox("normal","Button",WorkshopPanel(new Color("1b2b30"),new Color("3e5756")));
        theme.SetStylebox("hover","Button",WorkshopPanel(new Color("243b3e"),WorkshopCyan));
        theme.SetStylebox("pressed","Button",WorkshopPanel(new Color("102b2d"),WorkshopCopper));
        theme.SetStylebox("disabled","Button",WorkshopPanel(new Color("152124"),new Color("334440")));
        theme.SetStylebox("focus","Button",WorkshopPanel(new Color(0,0,0,0),WorkshopCopper,0));
        theme.SetStylebox("normal","OptionButton",WorkshopPanel(new Color("0e1b20"),new Color("52675d"),9));
        theme.SetStylebox("hover","OptionButton",WorkshopPanel(new Color("203639"),WorkshopCyan,9));
        foreach(string state in new[] {"tab_unselected","tab_selected","tab_hovered"})
        {
            bool selected=state=="tab_selected";
            var tab=WorkshopPanel(new Color(selected ? "23383b" : "101c20"),new Color(selected ? "71d8cd" : "344a49"),10);
            tab.BorderWidthTop=tab.BorderWidthLeft=tab.BorderWidthRight=0;
            theme.SetStylebox(state,"TabBar",tab); theme.SetStylebox(state,"TabContainer",tab);
        }
        theme.SetColor("font_selected_color","TabBar",WorkshopCyan);
        theme.SetColor("font_selected_color","TabContainer",WorkshopCyan);
        return theme;
    }
    private void ResizeShop()
    {
        float width=Mathf.Min(900,GetViewport().GetVisibleRect().Size.X-48);
        _shopCard.CustomMinimumSize=new Vector2(width,0);
        int columns=width>=820 ? 3 : 2;
        _skinGrid.Columns=columns; _skinScroll.CustomMinimumSize=new Vector2(columns*148+16,320);
        _equipmentGrid.Columns=columns==3 ? 2 : 1;
    }

    private void RefreshShop()
    {
        _walletLabel.Text=$"{SaveData.Wallet} монет";
        int owned=0; for(int i=0;i<CharacterCatalog.Count;i++) if(SaveData.OwnsSkin(i)) owned++;
        _collectionLabel.Text=$"КОЛЕКЦІЯ {owned} / {CharacterCatalog.Count}   ·   9 ФОРМ";
        for(int i=0;i<UpgradeCatalog.Count;i++)
        {
            int level=SaveData.UpgradeLevel(i);
            int price=UpgradeCatalog.Price(i,level);
            _shopItems[i].Text=$"{UpgradeCatalog.Name(i)}  {level}/3\n{UpgradeCatalog.Effect(i)}  ·  "+(level==3 ? "Максимум" : $"{price} монет");
            _shopItems[i].Disabled=level==3 || SaveData.Wallet<price;
        }
        for(int i=0;i<CharacterCatalog.Count;i++)
        {
            bool selected=SaveData.SelectedSkin==i,unlocked=SaveData.OwnsSkin(i),focused=_previewSkin==i;
            _skinLabels[i].Text=CharacterCatalog.Get(i).Name;
            _skinLabels[i].AddThemeColorOverride("font_color",focused ? new Color("f4ead4") : new Color("c4d1ca"));
            _skinPrices[i].Text=selected ? "● ОБРАНО" : unlocked ? "ВІДКРИТО" : $"{CharacterCatalog.Get(i).Price} монет";
            _skinPrices[i].AddThemeColorOverride("font_color",selected ? WorkshopCyan : unlocked ? new Color("91ada1") : WorkshopCopper);
            _skinItems[i].Visible=CharacterCatalog.Get(i).Form==CharacterCatalog.Get(_previewSkin).Form;
            _skinItems[i].AddThemeStyleboxOverride("normal",WorkshopPanel(new Color(focused ? "263d3e" : "18262b"),focused ? WorkshopCyan : selected ? WorkshopCopper : new Color("3a5050"),6));
        }
        var character=CharacterCatalog.Get(_previewSkin);
        _formFilter.Select((int)character.Form);
        int baseSkin=CharacterCatalog.BaseSkin(character.Form);
        bool locked=!SaveData.OwnsSkin(baseSkin);
        _heroForm.Text=CharacterCatalog.FormName(character.Form);
        _heroStatus.Text=SaveData.SelectedSkin==character.Id ? "У СПОРЯДЖЕННІ" : SaveData.OwnsSkin(character.Id) ? "Є У КОЛЕКЦІЇ" : locked && character.Id!=baseSkin ? "СПЕРШУ ВІДКРИЙ ФОРМУ" : $"ЦІНА · {character.Price} МОНЕТ";
        _unlockForm.Visible=locked && character.Id!=baseSkin;
        _unlockForm.Text=$"ВІДКРИТИ {CharacterCatalog.FormName(character.Form)}\n{SaveData.SkinPrice(baseSkin)} монет";
        _unlockForm.Disabled=SaveData.Wallet<SaveData.SkinPrice(baseSkin);
        _shopHero.Texture=CharacterCatalog.Texture(character.Id); _heroName.Text=character.Name;
        _buySkin.Text=locked && character.Id!=baseSkin ? $"СКІН · {character.Price}" : SaveData.SelectedSkin==character.Id ? "ВИБРАНО" : SaveData.OwnsSkin(character.Id) ? "ОБРАТИ" : $"КУПИТИ · {character.Price}";
        _buySkin.Disabled=(locked && character.Id!=baseSkin) || SaveData.SelectedSkin==character.Id || (!SaveData.OwnsSkin(character.Id) && SaveData.Wallet<character.Price);
        _menuHero.Texture=CharacterCatalog.Texture(SaveData.SelectedSkin);
        _trailPreview.StyleId=SaveData.SelectedTrail;
        for(int i=0;i<TrailStyle.Count;i++)
        {
            _trailItems[i].Text=TrailStyle.Name(i)+(SaveData.SelectedTrail==i ? "  ·  ОБРАНО" : SaveData.OwnsTrail(i) ? "" : "\n"+TrailStyle.Requirement(i));
            _trailItems[i].AddThemeStyleboxOverride("normal",Card(new Color(SaveData.SelectedTrail==i ? "293b34" : "172323"),SaveData.SelectedTrail==i ? Aqua : new Color("42574f"),3));
            _trailItems[i].Disabled=false;
        }
    }

    private void OpenShop()
    {
        _shopReturn=_finished ? _gameOverPanel : _started ? _pauseOverlay : _startMenuPanel;
        _shopReturn.Hide(); _previewSkin=SaveData.SelectedSkin;
        RefreshShop(); _shop.Show(); _shopBack.GrabFocus();
    }
    private void CloseShop()
    {
        _shop.Hide();
        _shopReturn?.Show();
        if(_started && !_finished) _resumeButton.GrabFocus();
        else if (_finished) GetNode<Button>("Control/GameOverPanel/RestartButton").GrabFocus();
        else { BuildMainMenu(); GetNode<Button>("Control/StartMenuPanel/StartButton").GrabFocus(); }
    }
    private void TogglePause()
    {
        if (!_started || _finished || _shop.Visible) return;
        SaveData.Save();
        GetTree().Paused = !GetTree().Paused;
        _pauseOverlay.Visible = GetTree().Paused; _pauseButton.Visible = !GetTree().Paused;
        if (GetTree().Paused) { _pauseStats.Text = $"{Mathf.FloorToInt(_totalDistance):N0} м  ·  коло {_lap}  ·  {_coins} монет"; _resumeButton.GrabFocus(); }
        else { SaveData.Save(); GetViewport().GuiReleaseFocus(); }
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsEcho()) return;
        if (_settings.Visible && (input.IsActionPressed(InputSetup.Back) || input.IsActionPressed(InputSetup.Pause))) CloseSettings();
        else if (_shop.Visible && (input.IsActionPressed(InputSetup.Back) || input.IsActionPressed(InputSetup.Pause)))
            CloseShop();
        else if (input.IsActionPressed(InputSetup.Pause)) TogglePause();
        else return;
        GetViewport().SetInputAsHandled();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && _started && !_finished && !GetTree().Paused
            && !DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.NoFocus)) TogglePause();
    }
    private void BuildVignette(Control root)
    {
        _vignette = new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
uniform float intensity = 0.0;
void fragment() {
    float d = length((UV-vec2(0.5))*vec2(1.15,1.0));
    COLOR = vec4(0.90,0.10,0.015,smoothstep(0.35,0.78,d)*intensity*0.50);
}" } };
        var rect = new ColorRect { Material = _vignette, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(rect); root.MoveChild(rect,0); rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
    public void UpdateScore(int score) { _coins = score; _scoreLabel.Text = $"МОНЕТИ: {score}"; }
    public void UpdateDepth(float depth) => _shownDepth = depth;
    public void UpdateRunStats(int lap, float distance)
    {
        _totalDistance = distance;
        if (_lap == lap) return;
        _lap = lap; _lapLabel.Text = $"КОЛО {lap}";
    }
    public void SetBonuses(float doubled,float magnet) { _doubleBonus = doubled; _magnetBonus = magnet; }
    public void SetHeatWarning(bool visible) => _heatLabel.Visible = visible;
    public void SetShield(bool active) => _shieldLabel.Visible = active;
    public void SetHeat(float level, bool burning)
    {
        _heatLevel = level; _burning = burning;
        _heatBar.Visible = burning || level > 0.02f; _heatBar.Value = level;
        _heatFill.BgColor = new Color("eec878").Lerp(new Color("fc5728"),level);
    }
    public void SetThermalStatus(float temperature, bool burning, float seconds, float cooling)
    {
        SetHeat(temperature,burning); _heatLabel.Visible = temperature > 0.02f || cooling > 0f;
        _heatLabel.Text = cooling > 0 ? $"ОХОЛОДЖЕНО  {cooling:0.0}с" : $"ПЕРЕГРІВ  {temperature*100:0}%";
        _heatLabel.AddThemeColorOverride("font_color",cooling > 0f ? Aqua : new Color("ffb06d"));
    }
    public void SetLanternCharge(float seconds,float capacity,bool wanted)
    {
        if(_lanternStatus==null) return;
        _lanternStatus.Visible=_started && !_finished;
        _chargeBar.MaxValue=capacity; _chargeBar.Value=seconds;
        _chargeText.Text=seconds<=0 ? "ЛІХТАР · ПОРОЖНЬО" : $"ЛІХТАР · {Mathf.CeilToInt(seconds)} с"+(wanted ? "" : " · ВИМК.");
        _chargeText.Modulate=seconds<3 ? new Color("ffb56a") : Colors.White;
    }
    public override void _Process(double delta)
    {
        if (GetTree().Paused) return;
        _announcementTime = Mathf.Max(0,_announcementTime-(float)delta);
        if (_announcementTime <= 0) _coreBanner.Hide();
        _pulseTime += (float)delta;
        float target = _finished ? 0 : _burning ? _heatLevel*(0.55f + SaveData.FlashIntensity*0.12f*Mathf.Sin(_pulseTime*6f)) : _heatLevel*0.35f;
        _vignetteLevel = Mathf.Lerp(_vignetteLevel,target,1f-Mathf.Exp(-(float)delta*4f));
        _vignette?.SetShaderParameter("intensity",_vignetteLevel);
        _uiTicker += (float)delta;
        if (_uiTicker < 0.1f) return;
        _uiTicker = 0f;
        _depthLabel.Text = $"ГЛИБИНА: {Mathf.FloorToInt(_shownDepth):N0} м";
        if (_started)
        {
            var route = GetParent() as LevelGenerator;
            float length = route?.TunnelLength ?? 40000f;
            _journeyBar.Value = Mathf.Clamp(_shownDepth/Mathf.Max(1,length),0,1);
            bool beforeCore = _shownDepth < (route?.CoreDepthPoint ?? 20000f);
            float remaining = (beforeCore ? route?.CoreDepthPoint ?? 20000f : length) - _shownDepth;
            _destinationLabel.Text = $"{(beforeCore ? "ДО ЯДРА" : "ДО ВИХОДУ")}  {Mathf.Max(0,remaining):N0} м";
            _zoneLabel.Text = route?.Zone switch { CaveZone.Crystal => "КРИСТАЛІЧНИЙ ПОЯС", CaveZone.Magma => "МАГМОВІ ТРІЩИНИ", CaveZone.Core => "СЕРЦЕ ПЛАНЕТИ", _ => "БАЗАЛЬТОВИЙ РОЗЛОМ" };
        }
        _bonusLabel.Text = (_doubleBonus > 0 ? $"МОНЕТИ ×2  {_doubleBonus:0}с\n" : "") + (_magnetBonus > 0 ? $"МАГНІТ  {_magnetBonus:0}с" : "");
        if (_started && !_finished && GetParent() is LevelGenerator level)
            _speedLabel.Text = $"ШВИДКІСТЬ  {level.FallSpeed:0}   /   {(level.CurrentDepth < level.CoreDepthPoint ? "ДО ЯДРА" : "ДО ВИХОДУ")}";
    }
    public void ShowGameOver(DeathCause cause = DeathCause.Collision)
    {
        if (_finished) return; _finished = true;
        bool record = SaveData.SubmitRun(_totalDistance,_coins,_lap);
        string reason = cause == DeathCause.Overheat ? "ПЕРЕГРІВ" : "ЗІТКНЕННЯ";
        string hint = cause == DeathCause.Overheat ? "Пройди крізь холодний газ біля стіни, щоб знизити перегрів."
            : "Щит поглинає один удар.";
        _gameOverStats.Text = $"{reason}\n\n{Mathf.FloorToInt(_totalDistance):N0} м  ·  {_coins} монет  ·  коло {_lap}\n" + (record ? "Новий рекорд\n" : "") + "\n" + hint;
        _pauseOverlay.Hide(); _pauseButton.Hide(); _heatLabel.Hide(); _heatBar.Hide(); _shieldLabel.Hide(); _hintLabel.Hide(); _bonusLabel.Hide();
        _journeyBar.Hide(); _destinationLabel.Hide(); _tutorialPanel.Hide(); _zoneLabel.Hide(); _coreBanner.Hide();
        _gameOverPanel.Show(); GetNode<Button>("Control/GameOverPanel/RestartButton").GrabFocus();
    }
    private void Restart() => Reload(true);
    public void AnnounceCore()
    {
        _coreBanner.Text = "ЯДРО ПРОЙДЕНО\nДалі — до іншого боку!";
        _announcementTime = 2.5f; _coreBanner.Show();
    }
    public void BeginTutorial()
    {
        _tutorialStep = 0; _tutorialActive = true;
        if (_tutorialPanel == null) return;
        _tutorialPanel.Visible = _started; UpdateTutorialText();
    }
    private void UpdateTutorialText()
    {
        _tutorialText.Text = _tutorialStep == 0 ? "1 / 3  Рухайся вбік: A / D, стрілки або лівий стік."
            : _tutorialStep == 1 ? "2 / 3  Наведи світло мишкою чи правим стіком. ЛКМ / Y перемикає ліхтар."
            : "3 / 3  Підійди до кріотріщини біля стіни й пройди крізь холодний газ.";
    }
    public void NotifyTutorialMove() { if (_tutorialActive && _tutorialStep == 0) { _tutorialStep = 1; UpdateTutorialText(); } }
    public void NotifyTutorialLight() { if (_tutorialActive && _tutorialStep == 1) { _tutorialStep = 2; UpdateTutorialText(); } }
    public void NotifyTutorialWater() { if (TutorialWaterPending) FinishTutorial(); }
    private void FinishTutorial() { _tutorialActive = false; _tutorialPanel.Hide(); SaveData.TutorialComplete = true; SaveData.Save(); }
    private void Reload(bool start)
    {
        SaveData.Save(); _restartImmediately = start; GetTree().Paused = false; GetTree().ReloadCurrentScene();
    }
}
