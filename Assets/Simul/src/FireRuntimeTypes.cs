// Assets/Scripts/Fire/FireRuntimeTypes.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct MaterialProfile
{
    public FireMaterial type;
    public int ignitionTemp;   // Температура вспышки (напр. 300)
    public int initialFuel;    // Запас топлива
    public float heatTransfer;   // Теплопроводность
    public int heatCapacity;    // Теплоемкость
    public int calorificValue;    // Сколько энегрии выделяет при сгорании
}

[Serializable]
public class FireEdge
{
    public int targetIndex;     // Публичный индекс для простоты
    public float conductivity;  // кооф отдачи энергии
}


[Serializable]
public class FireNode
{
    public Vector3 position;
    public FireMaterial materialType;
    public List<FireEdge> neighbors = new();
}


[Serializable]
public struct SimNode
{
    public Vector3 position; // позиция точки
    public float energy;     //внутреняя энергия
    public int ignitionTemp; //температура воспламенения
    public float fuel;       // Запас топлива
    public bool burning;     //Горит/негорит
    public int calorificValue;    // Сколько энегрии выделяет при сгорании
}

[Serializable]
public struct SimEdge
{
    public int targetIndex;    // индекс соседнего узла в глобальном массиве
    public float materialConductivity; // коэффициент передачи тепла зависит материала
    public float hightConductivity; // коэффициент передачи тепла зависит от разниц высот
}