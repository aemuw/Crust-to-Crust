using Godot;

public partial class Hud : CanvasLayer
{
    private Label _scoreLabel, _depthLabel, _heatLabel, _lapLabel, _shieldLabel, _hintLabel;
    private Label _gameOverStats, _walletLabel, _speedLabel, _pauseStats;
    private Control _gameOverPanel, _startMenuPanel, _pauseOverlay, _shop;
    private Button _pauseButton, _resumeButton, _shopBack;
    private readonly Button[] _shopItems = new Button[3], _skinItems = new Button[3];
    private readonly Button[] _trailItems = new Button[4];
    private ProgressBar _heatBar;
    private StyleBoxFlat _heatFill;
    private ShaderMaterial _vignette;
    private TextureRect _menuHero;
    private LineEdit _seedInput;
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
    private static readonly Color Ink = new Color("0d1820"), Edge = new Color("334652"), Aqua = new Color("87ddd5");
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
        theme.SetStylebox("normal","Button",Card(new Color("162b35"),Edge,10));
        theme.SetStylebox("hover","Button",Card(new Color("24434a"),Aqua,10));
        theme.SetStylebox("pressed","Button",Card(new Color("10252d"),Aqua,10));
        theme.SetStylebox("focus","Button",Card(new Color(0,0,0,0),Aqua,10));
        theme.SetStylebox("panel","PanelContainer",Card(Ink,Edge,16));
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
        var backdrop = Art(_startMenuPanel,GD.Load<Texture2D>("res://Textures/ArtRefresh/cave_basalt.png"),Vector2.Zero);
        backdrop.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        backdrop.Modulate = new Color(0.23f,0.29f,0.35f);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        FullShade(_startMenuPanel,0.42f);
        var eyebrow = Text(_startMenuPanel,"ПОДОРОЖ КРІЗЬ ПЛАНЕТУ",16,Aqua);
        eyebrow.AnchorLeft = 0.075f; eyebrow.AnchorRight = 0.075f; eyebrow.AnchorTop = 0.17f; eyebrow.AnchorBottom = 0.17f;
        var title = Text(_startMenuPanel,"CRUST\nTO CRUST",72,Colors.White);
        title.AddThemeFontOverride("font",GD.Load<Font>("res://Fonts/BebasNeue-Regular.ttf"));
        title.AnchorLeft = 0.075f; title.AnchorRight = 0.075f; title.AnchorTop = 0.22f; title.AnchorBottom = 0.22f;
        var description = Text(_startMenuPanel,"Пірнай у темряву. Знайди шлях.\nПройди крізь ядро та повернись.",21,new Color("b0c1c8"));
        description.AnchorLeft = 0.075f; description.AnchorRight = 0.075f; description.AnchorTop = 0.45f; description.AnchorBottom = 0.45f;
        _seedInput = new LineEdit { Name = "SeedInput", PlaceholderText = "Seed забігу (необов’язково)", MaxLength = 11 };
        _startMenuPanel.AddChild(_seedInput);
        _seedInput.AnchorLeft = 0.075f; _seedInput.AnchorRight = 0.40f; _seedInput.AnchorTop = 0.535f; _seedInput.AnchorBottom = 0.535f;
        _seedInput.OffsetBottom = 32;
        _seedError = Text(_startMenuPanel,"",14,new Color("ffb556"));
        _seedError.AnchorLeft = 0.075f; _seedError.AnchorTop = 0.87f;
        var start = Button(_startMenuPanel,"ПОЧАТИ ПАДІННЯ   →",StartRun,"StartButton");
        start.AnchorLeft = 0.075f; start.AnchorRight = 0.40f; start.AnchorTop = 0.59f; start.AnchorBottom = 0.59f;
        start.OffsetBottom = 54;
        start.AddThemeStyleboxOverride("normal",Card(new Color("245b60"),Aqua,10));
        var shop = Button(_startMenuPanel,"МАГАЗИН  /  СКІНИ",OpenShop);
        shop.AnchorLeft = 0.075f; shop.AnchorRight = 0.40f; shop.AnchorTop = 0.59f; shop.AnchorBottom = 0.59f;
        shop.OffsetTop = 66; shop.OffsetBottom = 114;
        var learn = Button(_startMenuPanel,"ПРОЙТИ НАВЧАННЯ",() => { BeginTutorial(); StartRun(); });
        learn.CustomMinimumSize = new Vector2(0,30); learn.AddThemeFontSizeOverride("font_size",15);
        learn.AnchorLeft = 0.075f; learn.AnchorRight = 0.40f; learn.AnchorTop = 0.765f; learn.AnchorBottom = 0.765f; learn.OffsetBottom = 30;
        var best = Text(_startMenuPanel,$"РЕКОРД  {Mathf.FloorToInt(SaveData.BestDistance):N0} м   /   МОНЕТИ  {SaveData.Wallet}",16,Aqua);
        best.AnchorLeft = 0.075f; best.AnchorRight = 0.075f; best.AnchorTop = 0.85f; best.AnchorBottom = 0.85f;
        var display = new PanelContainer(); _startMenuPanel.AddChild(display);
        display.AnchorLeft = 0.52f; display.AnchorRight = 0.92f; display.AnchorTop = 0.16f; display.AnchorBottom = 0.79f;
        display.AddThemeStyleboxOverride("panel",Card(new Color(0.06f,0.10f,0.14f,0.70f),Edge,24));
        var content = new VBoxContainer(); display.AddChild(content);
        var chapter = Text(content,"01  /  НА КРАЮ РОЗЛОМУ",15,Aqua); chapter.HorizontalAlignment = HorizontalAlignment.Center;
        _menuHero = Art(content,GD.Load<Texture2D>(SaveData.SkinPath(SaveData.SelectedSkin)),new Vector2(0,245));
        _menuHero.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var controls = Text(content,"A / D / лівий стік — рух\nМишка / правий стік — світло\nF / Y — ліхтар • ESC / START — пауза",16); controls.HorizontalAlignment = HorizontalAlignment.Center;
        var footer = Text(_startMenuPanel,"ТЕПЛО • ГРАВІТАЦІЯ • ТЕМРЯВА",13,new Color("7d969f"));
        footer.AnchorLeft = 0.075f; footer.AnchorRight = 0.075f; footer.AnchorTop = 0.92f; footer.AnchorBottom = 0.92f;
    }
    public void StartRun()
    {
        if (_started) return;
        var level = GetParent() as LevelGenerator;
        if (!string.IsNullOrWhiteSpace(_seedInput?.Text))
        {
            if (!int.TryParse(_seedInput.Text.Trim(),out int seed)) { _seedError.Text = "Введи ціле число від −2147483648 до 2147483647."; return; }
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
        Clear(_gameOverPanel); FullShade(_gameOverPanel,0.80f);
        var card = new Panel(); _gameOverPanel.AddChild(card);
        card.AddThemeStyleboxOverride("panel",Card(Ink,Edge,18));
        card.AnchorLeft = 0.5f; card.AnchorRight = 0.5f; card.AnchorTop = 0.5f; card.AnchorBottom = 0.5f;
        card.OffsetLeft = -280; card.OffsetRight = 280; card.OffsetTop = -330; card.OffsetBottom = 330;
        card.MouseFilter = Control.MouseFilterEnum.Ignore;
        PlaceEnd(Text(_gameOverPanel,"КІНЕЦЬ ЗАБІГУ",14,Aqua),-306,-280);
        PlaceEnd(Text(_gameOverPanel,"Ще одна спроба?",36,Colors.White),-268,-210);
        _gameOverStats = Text(_gameOverPanel,"",18); PlaceEnd(_gameOverStats,-196,36);
        PlaceEnd(Button(_gameOverPanel,"НОВИЙ ЗАБІГ",Restart,"RestartButton"),62,112);
        PlaceEnd(Button(_gameOverPanel,"ПОВТОРИТИ ЦЕЙ SEED",() => { LevelGenerator.RequestedSeed = (GetParent() as LevelGenerator)?.RunSeed; Reload(true); },"ReplaySeedButton"),124,174);
        PlaceEnd(Button(_gameOverPanel,"МАГАЗИН / СКІНИ",OpenShop),186,236);
        PlaceEnd(Button(_gameOverPanel,"ГОЛОВНЕ МЕНЮ",() => Reload(false)),248,298);
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
        _hintLabel = Text(root,"A / D — рух   •   Мишка — ліхтар / F — вимкнути   •   Вода / термокапсули охолоджують • Ланцюжки монет підказують обхід",17);
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
        _pauseButton = Button(root,"Ⅱ   ESC",TogglePause); _pauseButton.Hide();
        _pauseButton.AnchorLeft = 1; _pauseButton.AnchorRight = 1;
        _pauseButton.OffsetLeft = -134; _pauseButton.OffsetRight = -24; _pauseButton.OffsetTop = 20; _pauseButton.OffsetBottom = 62;
        _pauseOverlay = new Control { Name = "PauseOverlay", Visible = false }; root.AddChild(_pauseOverlay);
        _pauseOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); FullShade(_pauseOverlay,0.84f);
        var box = CenterCard(_pauseOverlay,500);
        Text(box,"CRUST TO CRUST / ПАУЗА",14,Aqua).HorizontalAlignment = HorizontalAlignment.Center;
        Text(box,"Переведи подих",36,Colors.White).HorizontalAlignment = HorizontalAlignment.Center;
        _pauseStats = Text(box,"",18); _pauseStats.HorizontalAlignment = HorizontalAlignment.Center;
        _resumeButton = Button(box,"ПРОДОВЖИТИ   →",TogglePause);
        Button(box,"ПОЧАТИ ЗНОВУ",Restart);
        Button(box,"ГОЛОВНЕ МЕНЮ",() => Reload(false));
        Text(box,"ЗВУК",13,Aqua);
        Volume(box,"Музика",SaveData.MusicVolume,v => SaveData.MusicVolume = v);
        Volume(box,"Ефекти",SaveData.SfxVolume,v => SaveData.SfxVolume = v);
        Text(box,"КОМФОРТ",13,Aqua);
        Volume(box,"Тряска",SaveData.CameraShake,v => SaveData.CameraShake = v);
        Volume(box,"Спалахи",SaveData.FlashIntensity,v => SaveData.FlashIntensity = v);
        var lightMode = new CheckButton { Text = "Економне світло (3 джерела)" };
        box.AddChild(lightMode); lightMode.ButtonPressed = SaveData.EconomyLighting;
        lightMode.Toggled += low => { SaveData.EconomyLighting = low; SaveData.Save(); if (GetParent() is LevelGenerator level) level.CrystalLightBudget = low ? 3 : 6; };
        Text(box,"Мишка / правий стік — світло. ESC / START — гра.",15,new Color("93aab5")).HorizontalAlignment = HorizontalAlignment.Center;
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
        _shop = new Control { Name = "Shop", Visible = false }; root.AddChild(_shop);
        _shop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); FullShade(_shop,0.96f);
        var box = CenterCard(_shop,980);
        var heading = new HBoxContainer(); box.AddChild(heading);
        Text(heading,"СПОРЯДЖЕННЯ / СКІНИ",26,Colors.White).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _walletLabel = Text(heading,"",21,Aqua);
        Text(box,"ПОКРАЩЕННЯ — ЗБЕРІГАЮТЬСЯ МІЖ ЗАБІГАМИ",13,new Color("91a8b1"));
        var upgrades = new HBoxContainer(); box.AddChild(upgrades); upgrades.AddThemeConstantOverride("separation",12);
        for (int i = 0; i < 3; i++)
        {
            int item = i; _shopItems[i] = Button(upgrades,"",() => { SaveData.Buy(item); RefreshShop(); });
            _shopItems[i].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _shopItems[i].CustomMinimumSize = new Vector2(0,84); _shopItems[i].AddThemeFontSizeOverride("font_size",16);
        }
        Text(box,"СКІНИ — ЛИШЕ ВИГЛЯД, БЕЗ ЗМІНИ ФІЗИКИ",13,new Color("91a8b1"));
        var skins = new HBoxContainer(); box.AddChild(skins); skins.AddThemeConstantOverride("separation",12);
        string[] skinNames = { "БАЗАЛЬТ", "КРИЖАНИЙ КВАРЦ", "ЖАР ОБСИДІАНУ" };
        for (int i = 0; i < 3; i++)
        {
            int skin = i;
            var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; skins.AddChild(panel);
            var column = new VBoxContainer(); panel.AddChild(column);
            Art(column,GD.Load<Texture2D>(SaveData.SkinPath(i)),new Vector2(0,108));
            Text(column,skinNames[i],16).HorizontalAlignment = HorizontalAlignment.Center;
            _skinItems[i] = Button(column,"",() => {
                SaveData.SelectOrBuySkin(skin); GetParent().GetNode<Player>("Player").ApplySkin(); RefreshShop();
            });
            _skinItems[i].AddThemeFontSizeOverride("font_size",16);
        }
        Text(box,"СЛІДИ ЗА МАЙСТЕРНІСТЬ",13,Aqua);
        for (int i = 0; i < 4; i++)
        {
            int trail = i;
            _trailItems[i] = Button(box,"",() => { SaveData.SelectTrail(trail); RefreshShop(); });
            _trailItems[i].AddThemeFontSizeOverride("font_size",16);
        }
        _shopBack = Button(box,"←  ПОВЕРНУТИСЬ  /  ESC / B",CloseShop);
    }
    private void RefreshShop()
    {
        _walletLabel.Text = $"{SaveData.Wallet} МОНЕТ";
        string[] names = {"ТЕПЛОІЗОЛЯЦІЯ","ОХОЛОДЖЕННЯ","ЛІХТАР"};
        string[] details = {"Нагрів −12% / рівень","Захист від жару +2с / рівень","Дальність +15% / рівень"};
        for (int i = 0; i < 3; i++)
        {
            int level = SaveData.UpgradeLevel(i);
            _shopItems[i].Text = $"{names[i]}  {level}/3\n{details[i]}\n" + (level == 3 ? "МАКСИМУМ" : $"{SaveData.Price(level)} МОНЕТ");
            _shopItems[i].Disabled = level == 3 || SaveData.Wallet < SaveData.Price(level);
            bool owned = SaveData.OwnsSkin(i);
            _skinItems[i].Text = SaveData.SelectedSkin == i ? "ВИБРАНО" : owned ? "ОБРАТИ" : $"КУПИТИ / {SaveData.SkinPrice(i)} МОНЕТ";
            _skinItems[i].Disabled = SaveData.SelectedSkin == i || (!owned && SaveData.Wallet < SaveData.SkinPrice(i));
        }
        _menuHero.Texture = GD.Load<Texture2D>(SaveData.SkinPath(SaveData.SelectedSkin));
        string[] trails = {"ПОВІТРЯНИЙ","БУРШТИНОВИЙ — пройди ядро","КВАРЦОВИЙ — пройди коло","ВОДЯНИЙ — погаси вогонь 3 рази за забіг"};
        for (int i = 0; i < 4; i++)
        {
            _trailItems[i].Text = (SaveData.SelectedTrail == i ? "✓  " : SaveData.OwnsTrail(i) ? "ОБРАТИ  " : "🔒  ") + trails[i];
            _trailItems[i].Disabled = !SaveData.OwnsTrail(i);
        }
    }
    private void OpenShop() { RefreshShop(); _shop.Show(); _shopBack.GrabFocus(); }
    private void CloseShop()
    {
        _shop.Hide();
        if (_finished) GetNode<Button>("Control/GameOverPanel/RestartButton").GrabFocus();
        else { BuildMainMenu(); GetNode<Button>("Control/StartMenuPanel/StartButton").GrabFocus(); }
    }
    private void TogglePause()
    {
        if (!_started || _finished || _shop.Visible) return;
        SaveData.Save();
        GetTree().Paused = !GetTree().Paused;
        _pauseOverlay.Visible = GetTree().Paused; _pauseButton.Visible = !GetTree().Paused;
        if (GetTree().Paused) { _pauseStats.Text = $"Глибина {Mathf.FloorToInt(_totalDistance):N0} м   /   Коло {_lap}   /   Монети {_coins}\nSeed {(GetParent() as LevelGenerator)?.RunSeed}"; _resumeButton.GrabFocus(); }
        else { SaveData.Save(); GetViewport().GuiReleaseFocus(); }
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsEcho()) return;
        if (_shop.Visible && (input.IsActionPressed(InputSetup.Back) || input.IsActionPressed(InputSetup.Pause)))
            CloseShop();
        else if (input.IsActionPressed(InputSetup.Pause)) TogglePause();
        else return;
        GetViewport().SetInputAsHandled();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && _started && !_finished && !GetTree().Paused) TogglePause();
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
        string hint = cause == DeathCause.Overheat ? "Вода або термокапсула гасять вогонь. Шукай світлий струмінь.\nТеплоізоляція сповільнює нагрівання."
            : "Рухайся за проходом між перешкодами.\nЩит поглинає один удар.";
        _gameOverStats.Text = $"{reason}\n{Mathf.FloorToInt(_totalDistance):N0} м   /   КОЛО {_lap}\nЗІБРАНО {_coins} МОНЕТ\n" + (record ? "НОВИЙ РЕКОРД!" : $"РЕКОРД {Mathf.FloorToInt(SaveData.BestDistance):N0} м") + $"\nSeed {(GetParent() as LevelGenerator)?.RunSeed}\n\n" + hint;
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
