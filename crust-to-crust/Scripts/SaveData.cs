using Godot;

/// <summary>Рекорди та налаштування гучності, що зберігаються між запусками (user://).</summary>
public static class SaveData
{
	private const string FilePath = "user://crust_save.cfg";
	private static bool _loaded;

	public static float BestDistance { get; private set; }
	public static int BestCoins { get; private set; }
	public static int BestLap { get; private set; } = 1;
	public static int Wallet { get; private set; }
    public static int Insulation { get; private set; }
    public static int Cooling { get; private set; }
    public static int Lantern { get; private set; }
    public static int Accumulator { get; private set; }
    public static int BatteryEfficiency { get; private set; }
    public static int Magnet { get; private set; }
    public static int ShieldRecovery { get; private set; }
    public static int SkinOwnedMask { get; private set; } = 1;
    public static int SelectedSkin { get; private set; }
    public static int SkinPrice(int skin) => CharacterCatalog.Get(skin).Price;
    public static bool OwnsSkin(int skin) => skin >= 0 && skin < CharacterCatalog.Count && (SkinOwnedMask & (1 << skin)) != 0;
    public static bool SelectOrBuySkin(int skin)
    {
        Load();
        if (skin < 0 || skin >= CharacterCatalog.Count) return false;
        if (!OwnsSkin(skin))
        {
            int baseSkin=CharacterCatalog.BaseSkin(CharacterCatalog.Get(skin).Form);
            if(skin!=baseSkin && !OwnsSkin(baseSkin)) return false;
            int price = SkinPrice(skin);
            if (Wallet < price) return false;
            Wallet -= price; SkinOwnedMask |= 1 << skin;
        }
        SelectedSkin = skin;
        Save();
        return true;
    }
    public static string SkinPath(int skin) => CharacterCatalog.Get(skin).TexturePath;
    public static int Price(int level) => 12 * (level + 1);
    public static int UpgradeLevel(int item) => item switch { 0=>Insulation,1=>Cooling,2=>Lantern,3=>Accumulator,4=>BatteryEfficiency,5=>Magnet,6=>ShieldRecovery,_=>3 };
    public static void ResetEquipment()
    {
        Load(); Insulation=Cooling=Lantern=Accumulator=BatteryEfficiency=Magnet=ShieldRecovery=0; Save();
    }
    public static bool Buy(int item)
    {
        Load();
        if (item < 0 || item >= UpgradeCatalog.Count) return false;
        int level = UpgradeLevel(item), price = UpgradeCatalog.Price(item,level);
        if (level >= 3 || Wallet < price) return false;
        Wallet -= price;
        switch(item) { case 0: Insulation++; break; case 1: Cooling++; break; case 2: Lantern++; break;
            case 3: Accumulator++; break; case 4: BatteryEfficiency++; break; case 5: Magnet++; break; case 6: ShieldRecovery++; break; }
        Save();
        return true;
    }
    // Wallet changes immediately in memory; disk writes are batched and flushed on normal exit.
    private static bool _dirty;
    private static float _saveTimer;
    public static void CreditCoin(int amount = 1) { Load(); Wallet += Mathf.Max(0,amount); _dirty = true; }
    public static void Tick(float dt)
    {
        if (!_dirty) return;
        _saveTimer += dt;
        if (_saveTimer >= 0.75f) Save();
    }

    public static bool EconomyLighting;
    public static float CameraShake = 1f, FlashIntensity = 1f;
    public static bool TutorialComplete;
    public static int MasteryMask, SelectedTrail;
    public static bool UnlockMastery(int bit)
    {
        Load(); if (bit < 0 || bit > 2 || (MasteryMask & (1 << bit)) != 0) return false;
        MasteryMask |= 1 << bit; Save(); return true;
    }
    public static bool OwnsTrail(int trail) => trail == 0 || (trail > 0 && trail <= 3 && (MasteryMask & (1 << (trail-1))) != 0);
    public static bool SelectTrail(int trail)
    { Load(); if (!OwnsTrail(trail)) return false; SelectedTrail = trail; Save(); return true; }
    public static float MusicVolume = 0.7f;
	public static float SfxVolume = 1f;

	public static void Load()
	{
		if (_loaded) return;
		_loaded = true;

		var cfg = new ConfigFile();
		if (cfg.Load(FilePath) != Error.Ok) return;

		Wallet = Mathf.Max(0, cfg.GetValue("shop", "wallet", 0).AsInt32());
        Insulation = Mathf.Clamp(cfg.GetValue("shop", "insulation", 0).AsInt32(), 0, 3);
        Cooling = Mathf.Clamp(cfg.GetValue("shop", "cooling", 0).AsInt32(), 0, 3);
        Lantern = Mathf.Clamp(cfg.GetValue("shop", "lantern", 0).AsInt32(), 0, 3);
        Accumulator=Mathf.Clamp(cfg.GetValue("shop","accumulator",0).AsInt32(),0,3);
        BatteryEfficiency=Mathf.Clamp(cfg.GetValue("shop","battery_efficiency",0).AsInt32(),0,3);
        Magnet=Mathf.Clamp(cfg.GetValue("shop","magnet",0).AsInt32(),0,3);
        ShieldRecovery=Mathf.Clamp(cfg.GetValue("shop","shield_recovery",0).AsInt32(),0,3);
        SkinOwnedMask = (cfg.GetValue("skins", "owned", 1).AsInt32() & ((1<<CharacterCatalog.Count)-1)) | 1;
        SelectedSkin = CharacterCatalog.Get(cfg.GetValue("skins", "selected", 0).AsInt32()).Id;
        if (!OwnsSkin(SelectedSkin)) SelectedSkin = 0;
        BestDistance = cfg.GetValue("best", "distance", 0f).AsSingle();
		BestCoins = cfg.GetValue("best", "coins", 0).AsInt32();
		BestLap = Mathf.Max(1, cfg.GetValue("best", "lap", 1).AsInt32());
		EconomyLighting = cfg.GetValue("graphics","economy_lighting",false).AsBool();
        CameraShake = Mathf.Clamp(cfg.GetValue("accessibility","camera_shake",1f).AsSingle(),0,1);
        FlashIntensity = Mathf.Clamp(cfg.GetValue("accessibility","flash_intensity",1f).AsSingle(),0,1);
        TutorialComplete = cfg.GetValue("progress","tutorial_complete",false).AsBool();
        MasteryMask = cfg.GetValue("progress","mastery",0).AsInt32() & 7;
        SelectedTrail = Mathf.Clamp(cfg.GetValue("skins","trail",0).AsInt32(),0,3);
        if (!OwnsTrail(SelectedTrail)) SelectedTrail = 0;
        MusicVolume = Mathf.Clamp(cfg.GetValue("audio", "music", 0.7f).AsSingle(), 0f, 1f);
		SfxVolume = Mathf.Clamp(cfg.GetValue("audio", "sfx", 1f).AsSingle(), 0f, 1f);
	}

	public static void Save()
	{
		_saveTimer = 0f;
        var cfg = new ConfigFile();
		cfg.SetValue("shop", "wallet", Wallet);
        cfg.SetValue("shop", "insulation", Insulation);
        cfg.SetValue("shop", "cooling", Cooling);
        cfg.SetValue("shop", "lantern", Lantern);
        cfg.SetValue("shop","accumulator",Accumulator);
        cfg.SetValue("shop","battery_efficiency",BatteryEfficiency);
        cfg.SetValue("shop","magnet",Magnet);
        cfg.SetValue("shop","shield_recovery",ShieldRecovery);
        cfg.SetValue("skins", "owned", SkinOwnedMask);
        cfg.SetValue("skins", "selected", SelectedSkin);
        cfg.SetValue("best", "distance", BestDistance);
		cfg.SetValue("best", "coins", BestCoins);
		cfg.SetValue("best", "lap", BestLap);
		cfg.SetValue("graphics","economy_lighting",EconomyLighting);
        cfg.SetValue("accessibility","camera_shake",CameraShake);
        cfg.SetValue("accessibility","flash_intensity",FlashIntensity);
        cfg.SetValue("progress","tutorial_complete",TutorialComplete);
        cfg.SetValue("progress","mastery",MasteryMask); cfg.SetValue("skins","trail",SelectedTrail);
        cfg.SetValue("audio", "music", MusicVolume);
		cfg.SetValue("audio", "sfx", SfxVolume);
		_dirty = cfg.Save(FilePath) != Error.Ok;
	}

	/// <summary>Повертає true, якщо пройдена дистанція стала новим рекордом.</summary>
	public static bool SubmitRun(float distance, int coins, int lap)
	{
		Load();
		bool record = distance > BestDistance;
		if (record) BestDistance = distance;
		if (coins > BestCoins) BestCoins = coins;
		if (lap > BestLap) BestLap = lap;
		Save();
		return record;
	}
}
