// Assets/Scripts/Fire/FireRuntimeTypes.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct MaterialProfile
{
    public FireMaterial type;
    public float ignitionTemp;   // Температура вспышки (напр. 300)
    public float thermalMass;    // Теплоемкость (чем выше, тем дольше греется)
    public float energyDensity;  // Энергия от горения (как сильно греет соседей)
    public float burnRate;       // Скорость пожирания топлива
    public float initialFuel;    // Запас топлива
}

[Serializable]
public class FireEdge
{
    public int targetIndex;     // Публичный индекс для простоты
    public float conductivity; // кооф отдачи энергии
}


[Serializable]
public class FireNode
{
    public Vector3 position; // мировые координаты
    public FireMaterial materialType;
    public List<FireEdge> neighbors = new();
    public float burnProgress; // 0..1
}


[Serializable]
public struct SimNode
{
    public Vector3 position;
    public float energy;
    public bool visited;
}

[Serializable]
public struct SimEdge
{
    public int targetIndex;    // индекс соседнего узла в глобальном массиве
    public float conductivity; // коэффициент передачи тепла (зависит от расстояния/материала)
}

/// <summary>
/// Простейший spatial hash: ключ = hash ячейки (int) для Dictionary.
/// Можно заменить на NativeMultiHashMap при переходе на Jobs.
/// </summary>
public static class SpatialHash
{
    // Простая хеш-функция для трёх координат (положительные/отрицательные поддерживаются)
    public static int CellKey(int x, int y, int z)
    {
        unchecked
        {
            int h = x;
            h = h * 73856093 ^ y * 19349663 ^ z * 83492791;
            return h;
        }
    }

    public static (int x, int y, int z) WorldToCell(Vector3 pos, float cellSize)
    {
        int x = Mathf.FloorToInt(pos.x / cellSize);
        int y = Mathf.FloorToInt(pos.y / cellSize);
        int z = Mathf.FloorToInt(pos.z / cellSize);
        return (x, y, z);
    }
}