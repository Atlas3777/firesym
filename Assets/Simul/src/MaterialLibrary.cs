using System.Collections.Generic;

public enum FireMaterial { Wood, Paper, Fabric, Metal, Concrete, Plastic
}

public class MaterialLibrary
{
    private static Dictionary<FireMaterial, MaterialProfile> _profiles;

    public static MaterialProfile GetProfile(FireMaterial type)
    {
        Ensure();
        return _profiles[type];
    }

    static void Ensure()
    {
        if (_profiles != null) return;
        _profiles = new Dictionary<FireMaterial, MaterialProfile>();

        // МЕТАЛЛ: Почти не горит, моментально передает чужое тепло дальше.
        _profiles[FireMaterial.Metal] = new MaterialProfile
        {
            type = FireMaterial.Metal,
            ignitionTemp = 1000,    // Почти нереально зажечь
            initialFuel = 1,        // Сгорает мгновенно (не топливо)
            calorificValue = 0,     // Не выделяет энергию при горении
            heatTransfer = 0.95f,   // Шикарно проводит тепло
            heatCapacity = 5     // Быстро нагревается и быстро остывает
        };

        // ДЕРЕВО: Классическое топливо.
        _profiles[FireMaterial.Wood] = new MaterialProfile
        {
            type = FireMaterial.Wood,
            ignitionTemp = 250,     // Средняя температура воспламенения
            initialFuel = 500,      // Горит долго
            calorificValue = 10,    // Хорошо греет соседей
            heatTransfer = 0.3f,    // Плохой проводник (горит локально)
            heatCapacity = 15     // Средне держит тепло
        };

        // БУМАГА: Вспыхивает мгновенно, быстро кончается.
        _profiles[FireMaterial.Paper] = new MaterialProfile
        {
            type = FireMaterial.Paper,
            ignitionTemp = 180, 
            initialFuel = 50,       // Сгорает очень быстро
            calorificValue = 15,    // Яркое, но короткое пламя
            heatTransfer = 0.2f,    
            heatCapacity = 1     // Моментальный нагрев
        };

        // ТКАНЬ: Что-то среднее между бумагой и деревом.
        _profiles[FireMaterial.Fabric] = new MaterialProfile
        {
            type = FireMaterial.Fabric,
            ignitionTemp = 210,
            initialFuel = 120,
            calorificValue = 8,
            heatTransfer = 0.2f,
            heatCapacity = 2
        };
        
        // ПЛАСТИК: Коварный материал.
        _profiles[FireMaterial.Plastic] = new MaterialProfile
        {
            type = FireMaterial.Plastic,
            ignitionTemp = 150,     // Легко плавится и горит
            initialFuel = 300,
            calorificValue = 25,    // Очень высокая энергия горения (жаркий)
            heatTransfer = 0.2f,    // Сам тепло проводит плохо
            heatCapacity = 8
        };

        // БЕТОН: Огнеупорный барьер.
        _profiles[FireMaterial.Concrete] = new MaterialProfile
        {
            type = FireMaterial.Concrete,
            ignitionTemp = 2000, 
            initialFuel = 0,        // Вообще не горит
            calorificValue = 0,
            heatTransfer = 0.1f,    // Ужасный проводник (служит теплоизолятором)
            heatCapacity = 40     // Очень инертный, нужно вечность греть
        };
    }
}