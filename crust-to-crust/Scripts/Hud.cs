using Godot;

public partial class Hud : CanvasLayer
{
    private Label _scoreLabel, _depthLabel, _heatLabel, _lapLabel, _shieldLabel, _hintLabel;
    private Label _gameOverStats, _walletLabel, _speedLabel, _pauseStats;
    private Control _gameOverPanel, _startMenuPanel, _pauseOverlay, _shop;
    private Button _pauseButton, _resumeButton, _shopBack;
    private readonly Button[] _shopItems = new Button[3], _skinItems = new Button[CharacterCatalog.Count];
    private readonly Button[] _trailItems = new Button[4];
    private ProgressBar _heatBar;
    private StyleBoxFlat _heatFill;
    private ShaderMaterial _vignette;
    private TextureRect _menuHero;
    private LineEdit _seedInput;
    private Control _settings;
    private Control _settingsReturn;
    private Control _shopReturn;
    private Button _settingsBack, _buySkin;
    private TextureRect _shopHero;
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
        theme.SetStylebox("normal","Button",Card(new Color("222928"),Edge,3));
        theme.SetStylebox("hover","Button",Card(new Color("343d36"),Aqua,3));
        theme.SetStylebox("pressed","Button",Card(new Color("1c231f"),Aqua,3));
        theme.SetStylebox("focus","Button",Card(new Color(0,0,0,0),Aqua,3));
        theme.SetStylebox("panel","PanelContainer",Card(Ink,Edge,4));
        return theme;
    }
    private static StyleBoxFlat Card(Color fill, Color edge, int radius)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = edge,
            ShadowColor = new Color(0,0,0,0.35f), ShadowSize = 14,
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
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0,48) };
        if (name != "") button.Name = name;
        parent.AddChild(button); button.Pressed += action; return button;
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
        var characters=Button(_startMenuPanel,"ПЕРСОНАЖІ",OpenShop);
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
        start.AddThemeStyleboxOverride("normal",Card(new Color("4b4d32"),Aqua,3));
        var halo=Art(_startMenuPanel,GameAssets.Halo,Vector2.Zero);
        halo.AnchorLeft=0.48f; halo.AnchorRight=0.93f; halo.AnchorTop=0.16f; halo.AnchorBottom=0.85f;
        halo.Modulate=new Color(0.28f,0.58f,0.56f,0.65f);
        _menuHero=Art(_startMenuPanel,CharacterCatalog.Texture(SaveData.SelectedSkin),Vector2.Zero);
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
        PlaceEnd(Button(_gameOverPanel,"ПЕРСОНАЖІ",OpenShop),104,152);
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
        _hintLabel = Text(root,"A / D · рух     Мишка · світло     F · ліхтар     Esc · пауза",16);
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
        Volume(box,"Музика",SaveData.MusicVolume,v=>SaveData.MusicVolume=v);
        Volume(box,"Ефекти",SaveData.SfxVolume,v=>SaveData.SfxVolume=v);
        Volume(box,"Тряска",SaveData.CameraShake,v=>SaveData.CameraShake=v);
        Volume(box,"Спалахи",SaveData.FlashIntensity,v=>SaveData.FlashIntensity=v);
        var light=new CheckButton { Text="Економне світло",ButtonPressed=SaveData.EconomyLighting };
        box.AddChild(light);
        light.Toggled+=low=>{ SaveData.EconomyLighting=low; if(GetParent() is LevelGenerator level) level.CrystalLightBudget=low ? 3 : 6; };
        var advanced=new VBoxContainer { Visible=false }; box.AddChild(advanced);
        var more=Button(box,"Маршрут і керування",()=>advanced.Visible=!advanced.Visible);
        box.MoveChild(more,box.GetChildCount()-2);
        Text(advanced,"A / D · рух    Мишка · світло\nF · ліхтар    Esc · пауза",15);
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
        row.AddChild(slider); slider.ValueChanged += v => { apply((float)v); AudioManager.ApplyVolumes(); SaveData.Save(); };
    }
    private void BuildShop(Control root)
    {
        _shop=new Control { Name="CharacterShop",Visible=false }; root.AddChild(_shop);
        _shop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); FullShade(_shop,0.97f);
        var box=CenterCard(_shop,640);
        var header=new HBoxContainer(); box.AddChild(header);
        Text(header,"МАЙСТЕРНЯ",28,new Color("eee5d2")).SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _walletLabel=Text(header,"",19,Aqua);
        var tabs=new TabContainer(); box.AddChild(tabs);
        var characters=new HBoxContainer { Name="Персонажі" }; tabs.AddChild(characters);
        characters.AddThemeConstantOverride("separation",24);
        var selection=new VBoxContainer { CustomMinimumSize=new Vector2(240,0) }; characters.AddChild(selection);
        for(int i=0;i<CharacterCatalog.Count;i++)
        {
            int id=i;
            _skinItems[i]=Button(selection,CharacterCatalog.Get(i).Name,()=>{ _previewSkin=id; RefreshShop(); });
            _skinItems[i].Alignment=HorizontalAlignment.Left;
        }
        var preview=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; characters.AddChild(preview);
        _shopHero=Art(preview,CharacterCatalog.Texture(0),new Vector2(200,245));
        _heroName=Text(preview,"",20); _heroName.HorizontalAlignment=HorizontalAlignment.Center;
        _buySkin=Button(preview,"",()=>{
            if(SaveData.SelectOrBuySkin(_previewSkin)) GetParent().GetNode<Player>("Player").ApplySkin();
            RefreshShop();
        });
        var equipment=new VBoxContainer { Name="Спорядження" }; tabs.AddChild(equipment);
        for(int i=0;i<3;i++)
        {
            int item=i; _shopItems[i]=Button(equipment,"",()=>{ SaveData.Buy(item); RefreshShop(); });
            _shopItems[i].CustomMinimumSize=new Vector2(0,78);
        }
        var trails=new VBoxContainer { Name="Сліди" }; tabs.AddChild(trails);
        for(int i=0;i<4;i++)
        {
            int id=i; _trailItems[i]=Button(trails,"",()=>{ SaveData.SelectTrail(id); RefreshShop(); });
            _trailItems[i].AddThemeFontSizeOverride("font_size",15);
        }
        _shopBack=Button(box,"НАЗАД",CloseShop);
    }

    private void RefreshShop()
    {
        _walletLabel.Text=$"{SaveData.Wallet} монет";
        string[] names={"Теплоізоляція","Охолодження","Ліхтар"};
        string[] effects={"Нагрів −12%","Захист +2 с","Дальність +15%"};
        for(int i=0;i<3;i++)
        {
            int level=SaveData.UpgradeLevel(i);
            _shopItems[i].Text=$"{names[i]}  {level}/3\n{effects[i]}  ·  "+(level==3 ? "Максимум" : $"{SaveData.Price(level)} монет");
            _shopItems[i].Disabled=level==3 || SaveData.Wallet<SaveData.Price(level);
        }
        for(int i=0;i<CharacterCatalog.Count;i++)
        {
            _skinItems[i].Text=CharacterCatalog.Get(i).Name+(SaveData.SelectedSkin==i ? "  ✓" : "");
            _skinItems[i].AddThemeColorOverride("font_color",_previewSkin==i ? Aqua : new Color("c6cec6"));
        }
        var character=CharacterCatalog.Get(_previewSkin);
        _shopHero.Texture=CharacterCatalog.Texture(character.Id); _heroName.Text=character.Name;
        _buySkin.Text=SaveData.SelectedSkin==character.Id ? "ВИБРАНО" : SaveData.OwnsSkin(character.Id) ? "ОБРАТИ" : $"КУПИТИ · {character.Price}";
        _buySkin.Disabled=SaveData.SelectedSkin==character.Id || (!SaveData.OwnsSkin(character.Id) && SaveData.Wallet<character.Price);
        _menuHero.Texture=CharacterCatalog.Texture(SaveData.SelectedSkin);
        string[] trails={"Повітряний","Бурштиновий · пройди ядро","Кварцовий · пройди коло","Водяний · погаси вогонь 3 рази"};
        for(int i=0;i<4;i++)
        {
            _trailItems[i].Text=(SaveData.SelectedTrail==i ? "✓  " : SaveData.OwnsTrail(i) ? "" : "·  ")+trails[i];
            _trailItems[i].Disabled=!SaveData.OwnsTrail(i);
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
        _heatLabel.Text = cooling > 0 ? $"ОХОЛОДЖЕНО  {cooling:0.0}с" : burning ? $"ГОРІННЯ — ШУКАЙ ОХОЛОДЖЕННЯ!  {seconds:0.0}с" : $"ТЕМПЕРАТУРА  {temperature*100:0}%";
        _heatLabel.AddThemeColorOverride("font_color",cooling > 0f ? Aqua : new Color("ffb06d"));
    }
    public override void _Process(double delta)
    {
        if (GetTree().Paused) return;
        _announcementTime = Mathf.Max(0,_announcementTime-(float)delta);
        if (_announcementTime <= 0) _coreBanner.Hide();
        _pulseTime += (float)delta;
        float target = _finished ? 0 : _burning ? 0.65f + SaveData.FlashIntensity*0.12f*Mathf.Sin(_pulseTime*6f) : _heatLevel*0.35f;
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
        string hint = cause == DeathCause.Overheat ? "Вода або термокапсула гасять вогонь."
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
            : _tutorialStep == 1 ? "2 / 3  Наведи світло мишкою чи правим стіком. F / Y перемикає ліхтар."
            : "3 / 3  Увійди в блакитний струмінь. Вода охолоджує та гасить вогонь.";
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
