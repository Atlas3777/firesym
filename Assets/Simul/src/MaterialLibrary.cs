using System.Collections.Generic;

public enum FireMaterial { Wood, Paper, Fabric, Metal, Concrete,
    Plastic
}

public class MaterialLibrary
{
    static Dictionary<FireMaterial, MaterialProfile> _profiles;

    static void Ensure()
    {
        if (_profiles != null) return;
        _profiles = new Dictionary<FireMaterial, MaterialProfile>();
        _profiles[FireMaterial.Fabric] = new MaterialProfile
        {
            type = FireMaterial.Fabric,
            ignitionTemp = 250f,
            thermalMass = 0.6f,
            energyDensity = 8f,
            burnRate = 1.8f,
            initialFuel = 0.8f
        };
        _profiles[FireMaterial.Wood] = new MaterialProfile
        {
            type = FireMaterial.Wood,
            ignitionTemp = 300f,
            thermalMass = 1.0f,
            energyDensity = 10f,
            burnRate = 1f,
            initialFuel = 1f
        };
        _profiles[FireMaterial.Plastic] = new MaterialProfile
        {
            type = FireMaterial.Plastic,
            ignitionTemp = 320f,
            thermalMass = 0.8f,
            energyDensity = 12f,
            burnRate = 1.2f,
            initialFuel = 0.9f
        };
    }

    public static MaterialProfile GetMaterialProfile(FireMaterial t)
    {
        Ensure();
        if (_profiles.TryGetValue(t, out var p)) return p;
        return _profiles[FireMaterial.Wood];
    }
}