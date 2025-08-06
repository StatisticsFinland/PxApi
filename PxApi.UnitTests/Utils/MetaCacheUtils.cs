using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Models;

namespace PxApi.UnitTests.Utils
{
    internal static class MetaCacheUtils
    {
        internal static int GetCacheKey(PxFileRef tableId)
        {
            // Use reflection to get the private META_SEED constant from MatrixCache
            System.Reflection.FieldInfo? field = typeof(DatabaseCache).GetField(
                "META_SEED",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?? throw new InvalidOperationException("META_SEED field not found on MatrixCache.");
            string metaSeed = (string)field.GetValue(null)!;

            // Calculate the same hash code that MatrixCache uses
            return HashCode.Combine(metaSeed, tableId);
        }

        internal static int GetCacheKey(IMatrixMap map)
        {
            // Use reflection to get the private META_SEED constant from MatrixCache
            System.Reflection.FieldInfo? field = typeof(DatabaseCache).GetField(
                "DATA_SEED",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?? throw new InvalidOperationException("DATA_SEED field not found on MatrixCache.");
            string dataSeed = (string)field.GetValue(null)!;

            // Use reflection to invoke the private static GenerateCacheKey method from MatrixCache
            System.Reflection.MethodInfo? method = typeof(DatabaseCache).GetMethod(
                "MapHash",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                [typeof(IMatrixMap)],
                null);
            int hash = method == null
                ? throw new InvalidOperationException("GenerateCacheKey(IMatrixMap) method not found on MatrixCache.")
                : (int)method.Invoke(null, [map])!;
            return HashCode.Combine(dataSeed, hash);
        }
    }
}
