public static class UpgradeCatalog
{
    public const int Count=7;
    public static string Name(int id)=>id switch
    {
        0=>"Теплоізоляція",1=>"Охолодження",2=>"Лінза ліхтаря",3=>"Акумулятор",
        4=>"Контакти батарейок",5=>"Магнітна котушка",6=>"Стабілізатор щита",_=>""
    };
    public static string Effect(int id)=>id switch
    {
        0=>"Нагрів −12%",1=>"Захист після води +2 с",2=>"Дальність +15%",
        3=>"Початковий заряд і місткість +2 с",4=>"Батарейка відновлює ще 0,5 с",
        5=>"Магніт +2 с і +20 px",6=>"Захист після удару в щит +0,2 с",_=>""
    };
    public static int Price(int id,int level)=>id<3 ? SaveData.Price(level) : 40*(level+1);
}
