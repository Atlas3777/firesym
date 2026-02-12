using UnityEngine;

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